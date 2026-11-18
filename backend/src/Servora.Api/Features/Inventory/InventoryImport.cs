using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Settings;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Inventory;

public class InventoryImportBatch
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public InventoryImportStatus Status { get; set; } = InventoryImportStatus.Validated;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorRows { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AppliedAt { get; set; }
    public List<InventoryImportRow> Rows { get; set; } = [];
}

public enum InventoryImportStatus
{
    Draft,
    Validated,
    FailedValidation,
    Applied,
    Cancelled
}

public class InventoryImportRow
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int BatchId { get; set; }
    public InventoryImportBatch Batch { get; set; } = null!;
    public int RowNumber { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? RawJson { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsValid { get; set; }
    public string? Action { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class InventoryImportBatchConfiguration : IEntityTypeConfiguration<InventoryImportBatch>
{
    public void Configure(EntityTypeBuilder<InventoryImportBatch> builder)
    {
        builder.ToTable("inventory_import_batches");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Id).ValueGeneratedOnAdd();
        builder.Property(batch => batch.FileName).IsRequired().HasMaxLength(255);
        builder.Property(batch => batch.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.HasIndex(batch => new { batch.ShopId, batch.CreatedAt });
        builder.HasOne(batch => batch.Shop).WithMany().HasForeignKey(batch => batch.ShopId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class InventoryImportRowConfiguration : IEntityTypeConfiguration<InventoryImportRow>
{
    public void Configure(EntityTypeBuilder<InventoryImportRow> builder)
    {
        builder.ToTable("inventory_import_rows");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).ValueGeneratedOnAdd();
        builder.Property(row => row.Sku).IsRequired().HasMaxLength(80);
        builder.Property(row => row.Name).IsRequired().HasMaxLength(180);
        builder.Property(row => row.CategoryName).HasMaxLength(120);
        builder.Property(row => row.RawJson).HasColumnType("json");
        builder.Property(row => row.ErrorMessage).HasMaxLength(1000);
        builder.Property(row => row.Action).HasMaxLength(30);
        builder.HasIndex(row => new { row.ShopId, row.BatchId });
        builder.HasOne(row => row.Batch).WithMany(batch => batch.Rows).HasForeignKey(row => row.BatchId).OnDelete(DeleteBehavior.Cascade);
    }
}

public static class InventoryImportEndpoints
{
    public sealed record ImportPreviewDto(int BatchId, int TotalRows, int ValidRows, int ErrorRows, IReadOnlyCollection<ImportRowDto> Rows);
    public sealed record ImportRowDto(int Id, int RowNumber, string Sku, string Name, string? CategoryName, bool IsValid, string? Action, string? ErrorMessage, ImportItemDraft? Item);
    public sealed record ImportApplyRequest(string Mode);
    public sealed record ImportApplyResult(int Created, int Updated, int Skipped);
    private sealed record InventoryImportSettings(decimal DefaultTaxPercent, decimal RoundingIncrement, string DefaultUnit, bool AllowDecimalQuantity, bool AutoCreateCategoryOnImport);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/inventory/imports/template", TemplateAsync)
            .WithName("DownloadInventoryImportTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapPost("/inventory/imports/preview", DryRunAsync)
            .WithName("PreviewInventoryImport")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapPost("/inventory/imports/dry-run", DryRunAsync)
            .WithName("DryRunInventoryImport")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapPost("/inventory/imports/{batchId:int}/apply", ApplyAsync)
            .WithName("ApplyInventoryImport")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);
    }

    private static Task<IResult> TemplateAsync(string? format)
    {
        var normalized = (format ?? "csv").Trim().ToLowerInvariant();
        if (normalized is "xlsx" or "excel")
        {
            return Task.FromResult<IResult>(Results.File(InventoryImportParser.BuildXlsxTemplate(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "inventory-import-template.xlsx"));
        }

        if (normalized != "csv") return Task.FromResult<IResult>(Results.BadRequest("Format must be csv or xlsx."));
        return Task.FromResult<IResult>(Results.File(Encoding.UTF8.GetBytes(InventoryImportParser.BuildCsvTemplate()), "text/csv", "inventory-import-template.csv"));
    }

    private static async Task<IResult> DryRunAsync([FromForm] IFormFile file, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        if (file.Length == 0) return Results.BadRequest("Import file is required.");

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var settings = await GetImportSettingsAsync(context, shopId, cancellationToken);
        var categories = await context.InventoryCategories
            .AsNoTracking()
            .Where(category => category.ShopId == shopId)
            .ToDictionaryAsync(category => category.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var existingSkus = await context.InventoryItems
            .AsNoTracking()
            .Where(item => item.ShopId == shopId)
            .Select(item => item.Sku)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        await using var stream = file.OpenReadStream();
        var parsedRows = InventoryImportParser.Parse(stream, file.FileName, categories.Keys, existingSkus, settings.DefaultTaxPercent, settings.DefaultUnit, settings.AllowDecimalQuantity, settings.AutoCreateCategoryOnImport);
        var batch = new InventoryImportBatch
        {
            ShopId = shopId,
            FileName = file.FileName,
            Status = parsedRows.Any(row => !row.IsValid) ? InventoryImportStatus.FailedValidation : InventoryImportStatus.Validated,
            TotalRows = parsedRows.Count,
            ValidRows = parsedRows.Count(row => row.IsValid),
            ErrorRows = parsedRows.Count(row => !row.IsValid),
            Rows = parsedRows.Select(row => new InventoryImportRow
            {
                ShopId = shopId,
                RowNumber = row.RowNumber,
                Sku = row.Item?.Sku ?? "",
                Name = row.Item?.Name ?? "",
                CategoryName = row.Item?.CategoryName,
                RawJson = row.Item is null ? null : JsonSerializer.Serialize(row.Item),
                ErrorMessage = row.ErrorMessage,
                IsValid = row.IsValid,
                Action = row.Action
            }).ToList()
        };

        context.InventoryImportBatches.Add(batch);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToPreview(batch));
    }

    private static async Task<IResult> ApplyAsync(int batchId, ImportApplyRequest request, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var mode = request.Mode.Trim().ToLowerInvariant();
        if (mode is not ("create" or "update" or "upsert")) return Results.BadRequest("Mode must be create, update, or upsert.");

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var batch = await context.InventoryImportBatches
            .Include(item => item.Rows)
            .FirstOrDefaultAsync(item => item.Id == batchId && item.ShopId == shopId, cancellationToken);
        if (batch is null) return Results.NotFound();
        if (batch.Status == InventoryImportStatus.Applied) return Results.BadRequest("Import batch was already applied.");

        var settings = await GetImportSettingsAsync(context, shopId, cancellationToken);
        var categories = await context.InventoryCategories.Where(category => category.ShopId == shopId).ToDictionaryAsync(category => category.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var existing = await context.InventoryItems.Where(item => item.ShopId == shopId).ToDictionaryAsync(item => item.Sku, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var created = 0;
        var updated = 0;
        var skipped = 0;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        foreach (var row in batch.Rows.Where(row => row.IsValid))
        {
            var draft = JsonSerializer.Deserialize<ImportItemDraft>(row.RawJson ?? "{}");
            if (draft is null) { skipped++; continue; }
            existing.TryGetValue(draft.Sku, out var item);
            if (item is null && mode == "update") { skipped++; continue; }
            if (item is not null && mode == "create") { skipped++; continue; }

            if (item is null)
            {
                item = new InventoryItem { ShopId = shopId, Sku = draft.Sku };
                context.InventoryItems.Add(item);
                existing[draft.Sku] = item;
                created++;
            }
            else
            {
                updated++;
            }

            if (!string.IsNullOrWhiteSpace(draft.CategoryName) && !categories.TryGetValue(draft.CategoryName, out var category) && settings.AutoCreateCategoryOnImport)
            {
                category = new InventoryCategory { ShopId = shopId, Name = draft.CategoryName.Trim(), IsActive = true };
                context.InventoryCategories.Add(category);
                categories[draft.CategoryName] = category;
            }

            ApplyDraft(item, draft, categories.TryGetValue(draft.CategoryName ?? "", out category) ? category.Id : null, settings);
            if (draft.StartingQuantity > 0)
            {
                item.OnHandQuantity += draft.StartingQuantity;
                item.StockVersion++;
                context.InventoryMovements.Add(new InventoryMovement
                {
                    ShopId = shopId,
                    InventoryItem = item,
                    Type = InventoryMovementType.StockIn,
                    Quantity = draft.StartingQuantity,
                    UnitCost = draft.PurchasePrice,
                    Reason = $"Inventory import batch #{batch.Id}"
                });
            }
        }

        batch.Status = InventoryImportStatus.Applied;
        batch.AppliedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new ImportApplyResult(created, updated, skipped));
    }

    private static void ApplyDraft(InventoryItem item, ImportItemDraft draft, int? categoryId, InventoryImportSettings settings)
    {
        var pricing = InventoryPricingCalculator.Calculate(new InventoryPricingInput(draft.PurchasePrice, draft.ShippingCost, draft.ServiceFee, draft.MarginPercent, draft.TaxPercent, draft.FixedSalePrice, settings.RoundingIncrement));
        item.CategoryId = categoryId;
        item.Name = draft.Name;
        item.Description = draft.Description;
        item.Unit = string.IsNullOrWhiteSpace(draft.Unit) ? "pcs" : draft.Unit;
        item.ItemType = draft.ItemType;
        item.Condition = draft.Condition;
        item.PurchasePrice = draft.PurchasePrice;
        item.ShippingCost = draft.ShippingCost;
        item.ServiceFee = draft.ServiceFee;
        item.MarginPercent = draft.MarginPercent;
        item.MarginAmount = pricing.MarginAmount;
        item.TaxPercent = draft.TaxPercent;
        item.TaxAmount = pricing.TaxAmount;
        item.PriceBeforeTax = pricing.PriceBeforeTax;
        item.PriceAfterTax = pricing.PriceAfterTax;
        item.FixedSalePrice = pricing.FixedSalePrice;
        item.CostPrice = draft.PurchasePrice;
        item.SalePrice = pricing.FixedSalePrice;
        item.ReorderLevel = draft.ReorderLevel;
        item.IsActive = draft.IsActive;
        item.Quality = draft.Quality;
        item.WarrantyDays = draft.WarrantyDays;
        item.SupplierName = draft.SupplierName;
        item.BarcodeValue = draft.BarcodeValue;
        item.LocationNote = draft.LocationNote;
    }

    private static async Task<InventoryImportSettings> GetImportSettingsAsync(AppDbContext context, int shopId, CancellationToken cancellationToken)
    {
        var values = await context.AppSettings
            .AsNoTracking()
            .Where(setting => setting.ShopId == shopId && ShopProfileSettingKeys.Defaults.Keys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

        string Value(string key) => values.GetValueOrDefault(key) ?? ShopProfileSettingKeys.Defaults[key];
        decimal DecimalValue(string key) => decimal.TryParse(Value(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : decimal.Parse(ShopProfileSettingKeys.Defaults[key], CultureInfo.InvariantCulture);
        bool BoolValue(string key) => bool.TryParse(Value(key), out var value)
            ? value
            : bool.Parse(ShopProfileSettingKeys.Defaults[key]);

        return new InventoryImportSettings(
            DecimalValue(ShopProfileSettingKeys.InventoryDefaultTaxPercent),
            DecimalValue(ShopProfileSettingKeys.InventoryRoundingIncrement),
            string.IsNullOrWhiteSpace(Value(ShopProfileSettingKeys.InventoryDefaultUnit)) ? "pcs" : Value(ShopProfileSettingKeys.InventoryDefaultUnit),
            BoolValue(ShopProfileSettingKeys.InventoryAllowDecimalQuantity),
            BoolValue(ShopProfileSettingKeys.InventoryAutoCreateCategoryOnImport));
    }

    private static ImportPreviewDto ToPreview(InventoryImportBatch batch) => new(
        batch.Id,
        batch.TotalRows,
        batch.ValidRows,
        batch.ErrorRows,
        batch.Rows.OrderBy(row => row.RowNumber).Select(row => new ImportRowDto(
            row.Id,
            row.RowNumber,
            row.Sku,
            row.Name,
            row.CategoryName,
            row.IsValid,
            row.Action,
            row.ErrorMessage,
            row.RawJson is null ? null : JsonSerializer.Deserialize<ImportItemDraft>(row.RawJson))).ToList());
}

public sealed record ParsedImportRow(int RowNumber, ImportItemDraft? Item, bool IsValid, string? Action, string? ErrorMessage);

public sealed record ImportItemDraft(
    string Sku,
    string Name,
    string? CategoryName,
    string? Description,
    string Unit,
    InventoryItemType ItemType,
    InventoryCondition Condition,
    decimal PurchasePrice,
    decimal ShippingCost,
    decimal ServiceFee,
    decimal MarginPercent,
    decimal TaxPercent,
    decimal? FixedSalePrice,
    decimal StartingQuantity,
    decimal ReorderLevel,
    bool IsActive,
    string? Quality,
    int? WarrantyDays,
    string? SupplierName,
    string? BarcodeValue,
    string? LocationNote);

public static class InventoryImportParser
{
    private static readonly string[] TemplateHeaders =
    [
        "SKU", "Nama Barang", "Kategori", "Tipe", "Kondisi", "Harga Beli", "Ongkir", "Jasa", "Margin (%)", "PPn (%)",
        "Stok Awal", "Satuan", "Reorder Level", "Quality", "Warranty Days", "Toko/Distributor", "Barcode", "Lokasi", "Deskripsi", "Harga Jual"
    ];

    private static readonly string[] TemplateSample =
    [
        "RAM-8GB-DDR4", "RAM DDR4 8GB", "Spare Part", "SparePart", "New", "150000", "10000", "25000", "20%", "11%",
        "5", "pcs", "2", "A", "30", "Distributor A", "899000000001", "Rak A1", "Contoh sparepart laptop", ""
    ];

    private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sku"] = ["sku", "item sku", "code", "kode"],
        ["name"] = ["name", "item", "item name", "nama barang", "nama"],
        ["category"] = ["category", "category name", "kategori"],
        ["description"] = ["description", "desc", "deskripsi"],
        ["unit"] = ["unit", "uom", "satuan"],
        ["itemType"] = ["item type", "type", "tipe"],
        ["condition"] = ["condition", "kondisi"],
        ["purchasePrice"] = ["purchase price", "cost price", "cost", "hpp", "harga beli"],
        ["shippingCost"] = ["shipping", "shipping cost", "ongkir"],
        ["serviceFee"] = ["service fee", "service", "jasa"],
        ["marginPercent"] = ["margin %", "margin percent", "margin", "margin (%)"],
        ["taxPercent"] = ["tax %", "tax percent", "tax", "ppn", "ppn (%)"],
        ["fixedSalePrice"] = ["fixed sale price", "sale price", "price", "harga jual"],
        ["startingQuantity"] = ["starting stock", "stock", "quantity", "on hand", "stok awal"],
        ["reorderLevel"] = ["reorder", "reorder level", "minimum stock"],
        ["isActive"] = ["active", "is active"],
        ["quality"] = ["quality"],
        ["warrantyDays"] = ["warranty days", "warranty"],
        ["supplierName"] = ["supplier", "supplier name", "toko/distributor", "distributor"],
        ["barcodeValue"] = ["barcode", "barcode value"],
        ["locationNote"] = ["location", "location note", "lokasi"]
    };

    public static IReadOnlyList<ParsedImportRow> Parse(
        Stream stream,
        string fileName,
        IEnumerable<string> categoryNames,
        ISet<string> existingSkus,
        decimal defaultTaxPercent = 11,
        string defaultUnit = "pcs",
        bool allowDecimalQuantity = true,
        bool autoCreateCategory = false)
    {
        var rows = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? ReadCsv(stream) : ReadXlsx(stream);
        if (rows.Count == 0) return [];

        var headers = rows[0].Select(Normalize).ToList();
        var index = HeaderAliases.ToDictionary(pair => pair.Key, pair => pair.Value.Select(alias => headers.IndexOf(Normalize(alias))).Where(i => i >= 0).DefaultIfEmpty(-1).First(), StringComparer.OrdinalIgnoreCase);
        var knownCategories = categoryNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fileSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ParsedImportRow>();

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            var errors = new List<string>();
            var sku = Get(row, index, "sku");
            var name = Get(row, index, "name");
            if (string.IsNullOrWhiteSpace(sku)) errors.Add("SKU is required.");
            if (string.IsNullOrWhiteSpace(name)) errors.Add("Name is required.");
            if (!string.IsNullOrWhiteSpace(sku) && !fileSkus.Add(sku)) errors.Add("Duplicate SKU in import file.");

            var categoryName = Get(row, index, "category");
            if (!string.IsNullOrWhiteSpace(categoryName) && !knownCategories.Contains(categoryName) && !autoCreateCategory) errors.Add("Category was not found.");
            if (!TryEnum(Get(row, index, "itemType"), InventoryItemType.SparePart, out InventoryItemType itemType)) errors.Add("Invalid item type.");
            if (!TryEnum(Get(row, index, "condition"), InventoryCondition.New, out InventoryCondition condition)) errors.Add("Invalid condition.");
            var purchasePrice = Money(Get(row, index, "purchasePrice"), errors, "Purchase price");
            var shippingCost = Money(Get(row, index, "shippingCost"), errors, "Shipping cost");
            var serviceFee = Money(Get(row, index, "serviceFee"), errors, "Service fee");
            var marginPercent = Money(Get(row, index, "marginPercent"), errors, "Margin percent");
            var taxPercent = Money(Get(row, index, "taxPercent"), errors, "Tax percent", defaultTaxPercent);
            var fixedSalePrice = NullableMoney(Get(row, index, "fixedSalePrice"), errors, "Fixed sale price");
            var startingQuantity = Money(Get(row, index, "startingQuantity"), errors, "Starting stock");
            var reorderLevel = Money(Get(row, index, "reorderLevel"), errors, "Reorder level");
            if (!allowDecimalQuantity && (HasFraction(startingQuantity) || HasFraction(reorderLevel))) errors.Add("Decimal quantities are not allowed.");
            var warrantyDays = NullableInt(Get(row, index, "warrantyDays"), errors, "Warranty days");
            if (warrantyDays is < 0) errors.Add("Warranty days cannot be negative.");

            var draft = new ImportItemDraft(
                sku.Trim(),
                name.Trim(),
                Null(categoryName),
                Null(Get(row, index, "description")),
                Null(Get(row, index, "unit")) ?? defaultUnit,
                itemType,
                condition,
                purchasePrice,
                shippingCost,
                serviceFee,
                marginPercent,
                taxPercent,
                fixedSalePrice,
                startingQuantity,
                reorderLevel,
                Bool(Get(row, index, "isActive"), true),
                Null(Get(row, index, "quality")),
                warrantyDays,
                Null(Get(row, index, "supplierName")),
                Null(Get(row, index, "barcodeValue")),
                Null(Get(row, index, "locationNote")));

            var action = existingSkus.Contains(draft.Sku) ? "Update" : "Create";
            result.Add(new ParsedImportRow(i + 1, draft, errors.Count == 0, action, errors.Count == 0 ? null : string.Join(" ", errors)));
        }

        return result;
    }

    public static string BuildCsvTemplate() => string.Join('\n', QuoteCsv(TemplateHeaders), QuoteCsv(TemplateSample));

    private static List<List<string>> ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        var rows = new List<List<string>>();
        while (reader.ReadLine() is { } line) rows.Add(ParseCsvLine(line));
        return rows;
    }

    private static List<List<string>> ReadXlsx(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var strings = ReadSharedStrings(archive);
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml") ?? throw new InvalidOperationException("First worksheet was not found.");
        using var sheetStream = sheet.Open();
        var doc = XDocument.Load(sheetStream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return doc.Descendants(ns + "row").Select(row =>
        {
            var values = new List<string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var index = CellColumnIndex(cell);
                while (values.Count < index) values.Add("");
                values.Add(CellText(cell, ns, strings));
            }
            return values;
        }).ToList();
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return doc.Descendants(ns + "si").Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value))).ToList();
    }

    private static string CellText(XElement cell, XNamespace ns, IReadOnlyList<string> sharedStrings)
    {
        var value = cell.Element(ns + "v")?.Value ?? "";
        return cell.Attribute("t")?.Value == "s" && int.TryParse(value, out var index) && index < sharedStrings.Count ? sharedStrings[index] : value;
    }

    public static byte[] BuildXlsxTemplate()
    {
        var values = TemplateHeaders.Concat(TemplateSample).Distinct().ToList();
        var index = values.Select((value, i) => new { value, i }).ToDictionary(item => item.value, item => item.i);
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
                </Types>
                """);
            AddZipEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            AddZipEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Inventory Import" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            AddZipEntry(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
                </Relationships>
                """);
            AddZipEntry(archive, "xl/sharedStrings.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="{values.Count}" uniqueCount="{values.Count}">
                {string.Join('\n', values.Select(value => $"  <si><t>{Xml(value)}</t></si>"))}
                </sst>
                """);
            AddZipEntry(archive, "xl/worksheets/sheet1.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    {XlsxRow(1, TemplateHeaders, index)}
                    {XlsxRow(2, TemplateSample, index)}
                  </sheetData>
                </worksheet>
                """);
        }
        return memory.ToArray();
    }

    private static string XlsxRow(int rowNumber, IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> sharedStringIndex) =>
        $"<row r=\"{rowNumber}\">{string.Concat(cells.Select((value, i) => $"<c r=\"{ColumnName(i)}{rowNumber}\" t=\"s\"><v>{sharedStringIndex[value]}</v></c>"))}</row>";

    private static string ColumnName(int index)
    {
        var name = "";
        for (index++; index > 0; index = (index - 1) / 26) name = (char)('A' + (index - 1) % 26) + name;
        return name;
    }

    private static void AddZipEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content.Trim());
    }

    private static string QuoteCsv(IEnumerable<string> cells) => string.Join(',', cells.Select(cell => $"\"{cell.Replace("\"", "\"\"")}\""));
    private static string Xml(string value) => System.Security.SecurityElement.Escape(value) ?? "";

    private static int CellColumnIndex(XElement cell)
    {
        var reference = cell.Attribute("r")?.Value ?? "";
        var letters = reference.TakeWhile(char.IsLetter).ToArray();
        if (letters.Length == 0) return 0;
        var index = 0;
        foreach (var letter in letters)
        {
            index = index * 26 + char.ToUpperInvariant(letter) - 'A' + 1;
        }
        return index - 1;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
            else if (ch == '"') quoted = !quoted;
            else if (ch == ',' && !quoted) { cells.Add(current.ToString()); current.Clear(); }
            else current.Append(ch);
        }
        cells.Add(current.ToString());
        return cells;
    }

    private static string Get(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> index, string key) => index.TryGetValue(key, out var i) && i >= 0 && i < row.Count ? row[i].Trim() : "";
    private static string Normalize(string value) => value.Trim().Replace("_", " ").Replace("-", " ").ToLowerInvariant();
    private static bool HasFraction(decimal value) => value != decimal.Truncate(value);
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool Bool(string value, bool fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value is "1" or "true" or "TRUE" or "yes" or "YES" or "on" or "ON";
    private static bool TryEnum<T>(string value, T fallback, out T parsed) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value)) { parsed = fallback; return true; }
        return Enum.TryParse(value.Replace(" ", ""), true, out parsed);
    }
    private static int? NullableInt(string value, List<string> errors, string label) => string.IsNullOrWhiteSpace(value) ? null : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : Error<int?>(errors, $"{label} is invalid.");
    private static decimal Money(string value, List<string> errors, string label, decimal fallback = 0) => string.IsNullOrWhiteSpace(value) ? fallback : NullableMoney(value, errors, label) ?? fallback;
    private static decimal? NullableMoney(string value, List<string> errors, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Replace("Rp", "", StringComparison.OrdinalIgnoreCase).Replace("$", "").Replace("%", "").Replace(",", "").Trim();
        if (!decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)) return Error<decimal?>(errors, $"{label} is invalid.");
        if (result < 0) errors.Add($"{label} cannot be negative.");
        return result;
    }
    private static T Error<T>(List<string> errors, string message)
    {
        errors.Add(message);
        return default!;
    }
}
