using System.IO.Compression;
using System.Text;
using Servora.Api.Features.Inventory;

namespace Servora.Api.UnitTests.Features.Inventory;

public class InventoryImportParserTests
{
    [Fact]
    public void Parse_ReturnsValidRowsForCsv()
    {
        using var stream = Csv("sku,name,category,purchase price,starting stock\nRAM-8GB,DDR4 8GB,Memory,150000,3");

        var rows = InventoryImportParser.Parse(stream, "items.csv", ["Memory"], new HashSet<string>());

        Assert.Single(rows);
        Assert.True(rows[0].IsValid);
        Assert.Equal("Create", rows[0].Action);
        Assert.Equal(3, rows[0].Item!.StartingQuantity);
    }

    [Fact]
    public void Parse_SupportsIndonesianHeaders()
    {
        using var stream = Csv("SKU,Nama Barang,Kategori,Harga Beli,Stok Awal\nRAM-16GB,DDR4 16GB,Memory,Rp250000,2");

        var rows = InventoryImportParser.Parse(stream, "items.csv", ["Memory"], new HashSet<string>());

        Assert.Single(rows);
        Assert.True(rows[0].IsValid);
        Assert.Equal(250000, rows[0].Item!.PurchasePrice);
        Assert.Equal(2, rows[0].Item!.StartingQuantity);
    }

    [Fact]
    public void Parse_AcceptsMarginPercentWithOrWithoutPercentSymbol()
    {
        using var stream = Csv("sku,name,margin\nRAM-8GB,DDR4 8GB,15\nRAM-16GB,DDR4 16GB,20%");

        var rows = InventoryImportParser.Parse(stream, "items.csv", [], new HashSet<string>());

        Assert.All(rows, row => Assert.True(row.IsValid));
        Assert.Equal(15, rows[0].Item!.MarginPercent);
        Assert.Equal(20, rows[1].Item!.MarginPercent);
    }

    [Fact]
    public void TemplateFiles_ParseAsValidImportRows()
    {
        using var csv = Csv(InventoryImportParser.BuildCsvTemplate());
        using var xlsx = new MemoryStream(InventoryImportParser.BuildXlsxTemplate());

        var csvRows = InventoryImportParser.Parse(csv, "template.csv", ["Spare Part"], new HashSet<string>());
        var xlsxRows = InventoryImportParser.Parse(xlsx, "template.xlsx", ["Spare Part"], new HashSet<string>());

        Assert.True(csvRows[0].IsValid, csvRows[0].ErrorMessage);
        Assert.True(xlsxRows[0].IsValid, xlsxRows[0].ErrorMessage);
        Assert.Equal("RAM-8GB-DDR4", xlsxRows[0].Item!.Sku);
    }

    [Fact]
    public void Parse_FlagsDuplicateSkuInFile()
    {
        using var stream = Csv("sku,name\nSSD-1TB,SSD\nSSD-1TB,SSD Duplicate");

        var rows = InventoryImportParser.Parse(stream, "items.csv", [], new HashSet<string>());

        Assert.False(rows[1].IsValid);
        Assert.Contains("Duplicate SKU", rows[1].ErrorMessage);
    }

    [Fact]
    public void Parse_FlagsInvalidNegativePrice()
    {
        using var stream = Csv("sku,name,purchase price\nBAT-1,Battery,-1");

        var rows = InventoryImportParser.Parse(stream, "items.csv", [], new HashSet<string>());

        Assert.False(rows[0].IsValid);
        Assert.Contains("Purchase price cannot be negative", rows[0].ErrorMessage);
    }

    [Fact]
    public void Parse_FlagsUnknownCategory()
    {
        using var stream = Csv("sku,name,category\nKB-1,Keyboard,Unknown");

        var rows = InventoryImportParser.Parse(stream, "items.csv", ["Keyboard"], new HashSet<string>());

        Assert.False(rows[0].IsValid);
        Assert.Contains("Category was not found", rows[0].ErrorMessage);
    }

    [Theory]
    [InlineData("Spare Part", "Baru", InventoryItemType.SparePart, InventoryCondition.New)]
    [InlineData("Suku Cadang", "Baru", InventoryItemType.SparePart, InventoryCondition.New)]
    [InlineData("Aksesori", "Bekas", InventoryItemType.Accessory, InventoryCondition.Used)]
    [InlineData("Part Bekas", "Refurbished", InventoryItemType.SecondhandPart, InventoryCondition.Refurbished)]
    [InlineData("Rakitan PC Bekas", "Untuk Kanibal", InventoryItemType.SecondhandPcBuild, InventoryCondition.ForParts)]
    [InlineData("Laptop Bekas", "Used", InventoryItemType.SecondhandLaptop, InventoryCondition.Used)]
    [InlineData("Konsumabel Servis", "New", InventoryItemType.ServiceConsumable, InventoryCondition.New)]
    [InlineData("Unit Rekondisi", "Refurbished", InventoryItemType.RefurbishedUnit, InventoryCondition.Refurbished)]
    [InlineData("Peralatan", "Baru", InventoryItemType.Equipment, InventoryCondition.New)]
    [InlineData("Alat / Perlengkapan", "New", InventoryItemType.ToolOrSupply, InventoryCondition.New)]
    public void Parse_SupportsIndonesianAndMultiIndustryItemTypesAndConditions(
        string rawType,
        string rawCondition,
        InventoryItemType expectedType,
        InventoryCondition expectedCondition)
    {
        using var stream = Csv($"SKU,Nama Barang,Kategori,Tipe,Kondisi,Harga Beli,Ongkir,Jasa,Margin (%),PPn (%),Stok Awal,Satuan,Reorder Level,Quality,Warranty Days,Toko/Distributor,Barcode,Lokasi,Deskripsi,Harga Jual\nPART-001,\"Replacement Belt & Filter\",HVAC & Mechanical,{rawType},{rawCondition},85000,30000,25000,10,11,0,pcs,,Original,365,,,,,155000");

        var rows = InventoryImportParser.Parse(stream, "items.csv", ["HVAC & Mechanical"], new HashSet<string>());

        var row = Assert.Single(rows);
        Assert.True(row.IsValid, row.ErrorMessage);
        Assert.Equal(expectedType, row.Item!.ItemType);
        Assert.Equal(expectedCondition, row.Item.Condition);
        Assert.Equal(155000m, row.Item.FixedSalePrice);
    }

    [Fact]
    public void Parse_RejectsNumericAndUnknownEnumValues()
    {
        using var stream = Csv("sku,name,tipe,kondisi\nSKU-1,Item,0,99");

        var rows = InventoryImportParser.Parse(stream, "items.csv", [], new HashSet<string>());

        var row = Assert.Single(rows);
        Assert.False(row.IsValid);
        Assert.Contains("Invalid item type.", row.ErrorMessage);
        Assert.Contains("Invalid condition.", row.ErrorMessage);
    }

    [Fact]
    public void Parse_ReadsXlsxWithInlineStringsAndNonDefaultFirstSheet()
    {
        using var xlsx = BuildInlineStringXlsx(
            "xl/worksheets/sheet2.xml",
            "worksheets/sheet2.xml",
            [
                ["SKU", "Nama Barang", "Kategori", "Tipe", "Kondisi", "Harga Beli"],
                ["PART-001", "Heavy Duty Belt Assembly", "Workshop Parts", "Spare Part", "Baru", "85000"]
            ]);

        var rows = InventoryImportParser.Parse(xlsx, "parts.xlsx", ["Workshop Parts"], new HashSet<string>());

        var row = Assert.Single(rows);
        Assert.True(row.IsValid, row.ErrorMessage);
        Assert.Equal("PART-001", row.Item!.Sku);
        Assert.Equal("Heavy Duty Belt Assembly", row.Item.Name);
        Assert.Equal(InventoryCondition.New, row.Item.Condition);
        Assert.Equal(85000m, row.Item.PurchasePrice);
    }

    [Theory]
    [InlineData("365", 365)]
    [InlineData("365.0", 365)]
    [InlineData("3.65E+2", 365)]
    [InlineData("365 days", 365)]
    [InlineData("365 hari", 365)]
    [InlineData("\"1,000\"", 1000)]
    [InlineData("", null)]
    public void Parse_AcceptsValidWarrantyDayFormats(string rawWarrantyDays, int? expected)
    {
        using var stream = Csv($"sku,name,warranty days\nPART-001,Part,{rawWarrantyDays}");

        var rows = InventoryImportParser.Parse(stream, "items.csv", [], new HashSet<string>());

        var row = Assert.Single(rows);
        Assert.True(row.IsValid, row.ErrorMessage);
        Assert.Equal(expected, row.Item!.WarrantyDays);
    }

    [Theory]
    [InlineData("365.5", "Warranty days is invalid.")]
    [InlineData("1 tahun", "Warranty days is invalid.")]
    [InlineData("-1", "Warranty days cannot be negative.")]
    public void Parse_RejectsInvalidWarrantyDays(string rawWarrantyDays, string expectedError)
    {
        using var stream = Csv($"sku,name,warranty days\nPART-001,Part,{rawWarrantyDays}");

        var rows = InventoryImportParser.Parse(stream, "items.csv", [], new HashSet<string>());

        var row = Assert.Single(rows);
        Assert.False(row.IsValid);
        Assert.Contains(expectedError, row.ErrorMessage);
    }

    [Fact]
    public void Parse_ValidatesMultipleRowsInXlsxWithFloatWarrantyDays()
    {
        var dataRows = new[]
        {
            new[] { "SKU", "Nama Barang", "Kategori", "Tipe", "Kondisi", "Harga Beli", "Ongkir", "Jasa", "Margin (%)", "PPn (%)", "Stok Awal", "Satuan", "Reorder Level", "Quality", "Warranty Days", "Toko/Distributor", "Barcode", "Lokasi", "Deskripsi", "Harga Jual" },
            new[] { "PART-001", "Capacitor 35uF", "Electrical", "Spare Part", "New", "85000", "30000", "25000", "10", "11", "0", "pcs", "", "Original", "365.0", "", "", "", "", "155000" },
            new[] { "PART-022", "Compressor Valve Kit", "Electrical", "Spare Part", "New", "4680000", "15000", "25000", "10", "11", "0", "pcs", "", "Original", "365", "", "", "", "", "5190000" }
        };
        using var xlsx = BuildNumericAndSharedStringXlsx(dataRows);

        var rows = InventoryImportParser.Parse(xlsx, "parts.xlsx", ["Electrical"], new HashSet<string>());

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.True(row.IsValid, row.ErrorMessage));
        Assert.All(rows, row => Assert.Equal(365, row.Item!.WarrantyDays));
    }

    private static MemoryStream BuildNumericAndSharedStringXlsx(string[][] rows)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            var rowXml = string.Concat(rows.Select((row, rIdx) =>
            {
                var rNum = rIdx + 1;
                var cells = string.Concat(row.Select((val, cIdx) =>
                {
                    if (string.IsNullOrEmpty(val)) return "";
                    var col = (char)('A' + cIdx);
                    var escaped = System.Security.SecurityElement.Escape(val);
                    return rIdx > 0 && (cIdx is >= 5 and <= 10 or 14 or 19)
                        ? $"<c r=\"{col}{rNum}\"><v>{escaped}</v></c>"
                        : $"<c r=\"{col}{rNum}\" t=\"inlineStr\"><is><t>{escaped}</t></is></c>";
                }));
                return $"<row r=\"{rNum}\">{cells}</row>";
            }));
            WriteEntry(archive, "xl/worksheets/sheet1.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>{rowXml}</sheetData>
                </worksheet>
                """);
        }
        memory.Position = 0;
        return memory;
    }

    private static MemoryStream Csv(string text) => new(Encoding.UTF8.GetBytes(text));

    private static MemoryStream BuildInlineStringXlsx(string entryPath, string relTarget, string[][] rows)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Parts" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="{relTarget}"/>
                </Relationships>
                """);
            var rowXml = string.Concat(rows.Select((row, rIdx) =>
            {
                var rNum = rIdx + 1;
                var cells = string.Concat(row.Select((val, cIdx) =>
                {
                    var col = (char)('A' + cIdx);
                    var escaped = System.Security.SecurityElement.Escape(val);
                    return $"<c r=\"{col}{rNum}\" t=\"inlineStr\"><is><t>{escaped}</t></is></c>";
                }));
                return $"<row r=\"{rNum}\">{cells}</row>";
            }));
            WriteEntry(archive, entryPath, $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>{rowXml}</sheetData>
                </worksheet>
                """);
        }
        memory.Position = 0;
        return memory;
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content.Trim());
    }
}
