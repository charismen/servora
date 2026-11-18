using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Assets;

public static class AssetEndpoints
{
    public sealed record AssetListItemDto(
        int Id,
        string AssetTag,
        string PublicToken,
        string Name,
        int? CategoryId,
        string? CategoryName,
        string? CompanyName,
        string? Department,
        string? Location,
        string? Brand,
        string? Model,
        string? SerialNumber,
        string Status,
        string Condition,
        int? AssignedUserId,
        string? AssignedUserName,
        DateTimeOffset PurchaseDate,
        DateTimeOffset InServiceDate,
        DateTimeOffset? WarrantyExpiresAt,
        decimal PurchaseCost,
        decimal SalvageValue,
        int UsefulLifeMonths,
        string DepreciationMethod,
        decimal? EstimatedSellingValue,
        AssetValuationBreakdown Valuation,
        DateTimeOffset CreatedAt);

    public sealed record AssetDetailDto(
        int Id,
        string AssetTag,
        string PublicToken,
        string Name,
        string? Description,
        int? CategoryId,
        string? CategoryName,
        string? CompanyName,
        string? Department,
        string? Location,
        string? Brand,
        string? Model,
        string? SerialNumber,
        string Status,
        string Condition,
        int? AssignedUserId,
        string? AssignedUserName,
        string? SupplierName,
        string? InvoiceNumber,
        DateTimeOffset PurchaseDate,
        DateTimeOffset InServiceDate,
        DateTimeOffset? WarrantyExpiresAt,
        decimal PurchaseCost,
        decimal SalvageValue,
        int UsefulLifeMonths,
        string DepreciationMethod,
        decimal? EstimatedSellingValue,
        DateTimeOffset? DisposedAt,
        decimal? ActualSoldPrice,
        string? DisposalNotes,
        AssetValuationBreakdown Valuation,
        IReadOnlyCollection<AssetDepreciationScheduleRow> DepreciationSchedule,
        IReadOnlyCollection<AssetCustodyLogDto> CustodyLogs,
        IReadOnlyCollection<AssetMaintenanceLogDto> MaintenanceLogs,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record PublicAssetDto(
        string AssetTag,
        string Name,
        string? CategoryName,
        string? CompanyName,
        string? Department,
        string? Location,
        string? Brand,
        string? Model,
        string? SerialNumber,
        string Status,
        string Condition,
        string? CustodianName,
        DateTimeOffset PurchaseDate,
        DateTimeOffset? WarrantyExpiresAt,
        bool IsWarrantyActive);

    public sealed record AssetCustodyLogDto(
        int Id,
        int? UserId,
        string? UserName,
        string ActionType,
        string? Location,
        string Condition,
        string? Notes,
        string? RecordedByUserName,
        DateTimeOffset CreatedAt);

    public sealed record AssetMaintenanceLogDto(
        int Id,
        int? ServiceTicketId,
        string? TicketNumber,
        string MaintenanceType,
        string Title,
        string? Description,
        decimal Cost,
        string? PerformedBy,
        DateTimeOffset PerformedAt,
        DateTimeOffset? NextDueAt,
        string? RecordedByUserName,
        DateTimeOffset CreatedAt);

    public sealed record AssetCategoryDto(
        int Id,
        string Name,
        string? Description,
        int DefaultUsefulLifeMonths,
        decimal DefaultSalvagePercent,
        bool IsActive,
        int AssetCount);

    public sealed record AssetsSummaryDto(
        int TotalAssets,
        int ActiveCount,
        int AvailableCount,
        int UnderMaintenanceCount,
        int EolApproachingCount,
        decimal TotalPurchaseCost,
        decimal TotalNetBookValue,
        decimal TotalAccumulatedDepreciation,
        decimal MonthlyDepreciationExpense,
        decimal TotalEstimatedSellingValue,
        decimal TotalMaintenanceCost);

    public sealed record AssetsPageDto(
        IReadOnlyCollection<AssetListItemDto> Items,
        AssetsSummaryDto Summary,
        int TotalCount,
        int Page,
        int PageSize);

    public sealed record CreateAssetRequest(
        string? AssetTag,
        string Name,
        int? CategoryId,
        string? Description,
        string? CompanyName,
        string? Department,
        string? Location,
        string? Brand,
        string? Model,
        string? SerialNumber,
        string? Status,
        string? Condition,
        int? AssignedUserId,
        string? SupplierName,
        string? InvoiceNumber,
        DateTimeOffset? PurchaseDate,
        DateTimeOffset? InServiceDate,
        DateTimeOffset? WarrantyExpiresAt,
        decimal PurchaseCost,
        decimal? SalvageValue,
        int? UsefulLifeMonths,
        string? DepreciationMethod,
        decimal? EstimatedSellingValue);

    public sealed record UpdateAssetRequest(
        string? AssetTag,
        string Name,
        int? CategoryId,
        string? Description,
        string? CompanyName,
        string? Department,
        string? Location,
        string? Brand,
        string? Model,
        string? SerialNumber,
        string? Status,
        string? Condition,
        int? AssignedUserId,
        string? SupplierName,
        string? InvoiceNumber,
        DateTimeOffset? PurchaseDate,
        DateTimeOffset? InServiceDate,
        DateTimeOffset? WarrantyExpiresAt,
        decimal PurchaseCost,
        decimal? SalvageValue,
        int? UsefulLifeMonths,
        string? DepreciationMethod,
        decimal? EstimatedSellingValue);

    public sealed record UpdateCustodyRequest(
        int? UserId,
        string ActionType,
        string? Location,
        string? Condition,
        string? Notes);

    public sealed record AddMaintenanceLogRequest(
        int? ServiceTicketId,
        string MaintenanceType,
        string Title,
        string? Description,
        decimal Cost,
        string? PerformedBy,
        DateTimeOffset? PerformedAt,
        DateTimeOffset? NextDueAt,
        bool? SetUnderMaintenance);

    public sealed record DisposeAssetRequest(
        string Status,
        DateTimeOffset? DisposedAt,
        decimal? ActualSoldPrice,
        string? DisposalNotes);

    public sealed record UpsertAssetCategoryRequest(
        string Name,
        string? Description,
        int? DefaultUsefulLifeMonths,
        decimal? DefaultSalvagePercent,
        bool? IsActive);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/assets", ListAssetsAsync)
            .WithName("GetAssets")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsRead);

        app.MapGet("/assets/summary", GetAssetsSummaryAsync)
            .WithName("GetAssetsSummary")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsRead);

        app.MapGet("/assets/export", ExportAssetsAsync)
            .WithName("ExportAssets")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsRead);

        app.MapGet("/assets/categories", ListCategoriesAsync)
            .WithName("GetAssetCategories")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsRead);

        app.MapPost("/assets/categories", CreateCategoryAsync)
            .WithName("CreateAssetCategory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsWrite);

        app.MapPut("/assets/categories/{id:int}", UpdateCategoryAsync)
            .WithName("UpdateAssetCategory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsWrite);

        app.MapDelete("/assets/categories/{id:int}", DeleteCategoryAsync)
            .WithName("DeleteAssetCategory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsDelete);

        app.MapGet("/assets/{id:int}", GetAssetAsync)
            .WithName("GetAssetDetail")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsRead);

        app.MapGet("/assets/public/{token}", GetPublicAssetAsync)
            .WithName("GetPublicAsset")
            .WithOpenApi()
            .AllowAnonymous();

        app.MapPost("/assets", CreateAssetAsync)
            .WithName("CreateAsset")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsWrite);

        app.MapPut("/assets/{id:int}", UpdateAssetAsync)
            .WithName("UpdateAsset")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsWrite);

        app.MapDelete("/assets/{id:int}", DeleteAssetAsync)
            .WithName("DeleteAsset")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsDelete);

        app.MapPost("/assets/{id:int}/custody", UpdateCustodyAsync)
            .WithName("UpdateAssetCustody")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsWrite);

        app.MapPost("/assets/{id:int}/maintenance", AddMaintenanceLogAsync)
            .WithName("AddAssetMaintenanceLog")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsWrite);

        app.MapPost("/assets/{id:int}/dispose", DisposeAssetAsync)
            .WithName("DisposeAsset")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AssetsWrite);
    }

    private static async Task<IResult> ListAssetsAsync(
        string? search,
        int? categoryId,
        string? status,
        string? condition,
        int? assignedUserId,
        string? companyName,
        bool? eolOnly,
        string? sort,
        int? page,
        int? pageSize,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var baseQuery = context.Assets
            .AsNoTracking()
            .Where(a => a.ShopId == shopId && a.IsActive);

        // Compute summary over all active shop assets
        var allShopAssets = await baseQuery
            .Include(a => a.MaintenanceLogs)
            .ToListAsync(cancellationToken);

        var summary = CalculateSummary(allShopAssets);

        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a =>
                a.AssetTag.Contains(term) ||
                a.Name.Contains(term) ||
                (a.Brand != null && a.Brand.Contains(term)) ||
                (a.Model != null && a.Model.Contains(term)) ||
                (a.SerialNumber != null && a.SerialNumber.Contains(term)) ||
                (a.Location != null && a.Location.Contains(term)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(a => a.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AssetStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(condition) && Enum.TryParse<AssetCondition>(condition, true, out var parsedCondition))
        {
            query = query.Where(a => a.Condition == parsedCondition);
        }

        if (assignedUserId.HasValue)
        {
            query = query.Where(a => a.AssignedUserId == assignedUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(companyName))
        {
            var comp = companyName.Trim();
            query = query.Where(a => a.CompanyName != null && a.CompanyName.Contains(comp));
        }

        var assets = await query
            .Include(a => a.Category)
            .Include(a => a.AssignedUser)
            .Include(a => a.MaintenanceLogs)
            .ToListAsync(cancellationToken);

        // In-memory mapping to compute live valuation
        var items = assets.Select(a =>
        {
            var maintenanceTotal = a.MaintenanceLogs.Sum(m => m.Cost);
            var valuation = AssetValuationCalculator.Calculate(a, maintenanceTotal);
            var assignedName = a.AssignedUser != null ? $"{a.AssignedUser.FirstName} {a.AssignedUser.LastName}".Trim() : null;
            return new AssetListItemDto(
                a.Id,
                a.AssetTag,
                a.PublicToken,
                a.Name,
                a.CategoryId,
                a.Category?.Name,
                a.CompanyName,
                a.Department,
                a.Location,
                a.Brand,
                a.Model,
                a.SerialNumber,
                a.Status.ToString(),
                a.Condition.ToString(),
                a.AssignedUserId,
                assignedName,
                a.PurchaseDate,
                a.InServiceDate,
                a.WarrantyExpiresAt,
                a.PurchaseCost,
                a.SalvageValue,
                a.UsefulLifeMonths,
                a.DepreciationMethod.ToString(),
                a.EstimatedSellingValue,
                valuation,
                a.CreatedAt);
        });

        if (eolOnly == true)
        {
            items = items.Where(i => i.Valuation.IsEolApproaching || i.Valuation.IsFullyDepreciated);
        }

        items = sort?.ToLowerInvariant() switch
        {
            "tag-desc" => items.OrderByDescending(i => i.AssetTag),
            "name" => items.OrderBy(i => i.Name),
            "cost-desc" => items.OrderByDescending(i => i.PurchaseCost),
            "bookvalue-desc" => items.OrderByDescending(i => i.Valuation.CurrentBookValue),
            "age-desc" => items.OrderByDescending(i => i.Valuation.AgeMonths),
            _ => items.OrderBy(i => i.AssetTag)
        };

        var itemList = items.ToList();
        var totalCount = itemList.Count;
        var currentPage = Math.Max(1, page ?? 1);
        var currentPageSize = Math.Clamp(pageSize ?? 50, 1, 100);

        var pagedItems = itemList
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .ToList();

        return Results.Ok(new AssetsPageDto(pagedItems, summary, totalCount, currentPage, currentPageSize));
    }

    private static async Task<IResult> GetAssetsSummaryAsync(
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var assets = await context.Assets
            .AsNoTracking()
            .Where(a => a.ShopId == shopId && a.IsActive)
            .Include(a => a.MaintenanceLogs)
            .ToListAsync(cancellationToken);

        return Results.Ok(CalculateSummary(assets));
    }

    private static async Task<IResult> ExportAssetsAsync(
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var assets = await context.Assets
            .AsNoTracking()
            .Where(a => a.ShopId == shopId && a.IsActive)
            .Include(a => a.Category)
            .Include(a => a.AssignedUser)
            .Include(a => a.MaintenanceLogs)
            .OrderBy(a => a.AssetTag)
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Asset Tag,Name,Category,Company Tag,Department,Location,Brand,Model,Serial Number,Status,Condition,Custodian,Purchase Date,Useful Life (Mos),Age (Mos),Purchase Cost,Salvage Value,Monthly Deprec,Accumulated Deprec,Current Book Value,Estimated Selling Value,Total Maintenance,Net TCO");

        foreach (var a in assets)
        {
            var maintenanceTotal = a.MaintenanceLogs.Sum(m => m.Cost);
            var v = AssetValuationCalculator.Calculate(a, maintenanceTotal);
            var custodian = a.AssignedUser != null ? $"{a.AssignedUser.FirstName} {a.AssignedUser.LastName}".Trim() : "";

            csv.AppendLine(string.Join(',',
                Csv(a.AssetTag),
                Csv(a.Name),
                Csv(a.Category?.Name ?? ""),
                Csv(a.CompanyName ?? ""),
                Csv(a.Department ?? ""),
                Csv(a.Location ?? ""),
                Csv(a.Brand ?? ""),
                Csv(a.Model ?? ""),
                Csv(a.SerialNumber ?? ""),
                Csv(a.Status.ToString()),
                Csv(a.Condition.ToString()),
                Csv(custodian),
                Csv(a.PurchaseDate.ToString("yyyy-MM-dd")),
                a.UsefulLifeMonths.ToString(CultureInfo.InvariantCulture),
                v.AgeMonths.ToString(CultureInfo.InvariantCulture),
                v.PurchaseCost.ToString(CultureInfo.InvariantCulture),
                v.SalvageValue.ToString(CultureInfo.InvariantCulture),
                v.MonthlyDepreciation.ToString(CultureInfo.InvariantCulture),
                v.AccumulatedDepreciation.ToString(CultureInfo.InvariantCulture),
                v.CurrentBookValue.ToString(CultureInfo.InvariantCulture),
                v.EffectiveSellingValue.ToString(CultureInfo.InvariantCulture),
                v.TotalMaintenanceCost.ToString(CultureInfo.InvariantCulture),
                v.NetTco.ToString(CultureInfo.InvariantCulture)));
        }

        return Results.File(
            Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv",
            $"assets-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static async Task<IResult> GetAssetAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var asset = await context.Assets
            .AsNoTracking()
            .Include(a => a.Category)
            .Include(a => a.AssignedUser)
            .Include(a => a.CustodyLogs).ThenInclude(l => l.User)
            .Include(a => a.CustodyLogs).ThenInclude(l => l.RecordedByUser)
            .Include(a => a.MaintenanceLogs).ThenInclude(m => m.ServiceTicket)
            .Include(a => a.MaintenanceLogs).ThenInclude(m => m.RecordedByUser)
            .FirstOrDefaultAsync(a => a.Id == id && a.ShopId == shopId && a.IsActive, cancellationToken);

        if (asset is null) return Results.NotFound();

        var totalMaintenance = asset.MaintenanceLogs.Sum(m => m.Cost);
        var valuation = AssetValuationCalculator.Calculate(asset, totalMaintenance);
        var schedule = AssetValuationCalculator.BuildAnnualSchedule(asset);

        var assignedName = asset.AssignedUser != null ? $"{asset.AssignedUser.FirstName} {asset.AssignedUser.LastName}".Trim() : null;

        var custodyDtos = asset.CustodyLogs
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new AssetCustodyLogDto(
                l.Id,
                l.UserId,
                l.User != null ? $"{l.User.FirstName} {l.User.LastName}".Trim() : null,
                l.ActionType.ToString(),
                l.Location,
                l.Condition.ToString(),
                l.Notes,
                l.RecordedByUser != null ? $"{l.RecordedByUser.FirstName} {l.RecordedByUser.LastName}".Trim() : null,
                l.CreatedAt))
            .ToList();

        var maintenanceDtos = asset.MaintenanceLogs
            .OrderByDescending(m => m.PerformedAt)
            .Select(m => new AssetMaintenanceLogDto(
                m.Id,
                m.ServiceTicketId,
                m.ServiceTicket?.TicketNumber,
                m.MaintenanceType.ToString(),
                m.Title,
                m.Description,
                m.Cost,
                m.PerformedBy,
                m.PerformedAt,
                m.NextDueAt,
                m.RecordedByUser != null ? $"{m.RecordedByUser.FirstName} {m.RecordedByUser.LastName}".Trim() : null,
                m.CreatedAt))
            .ToList();

        return Results.Ok(new AssetDetailDto(
            asset.Id,
            asset.AssetTag,
            asset.PublicToken,
            asset.Name,
            asset.Description,
            asset.CategoryId,
            asset.Category?.Name,
            asset.CompanyName,
            asset.Department,
            asset.Location,
            asset.Brand,
            asset.Model,
            asset.SerialNumber,
            asset.Status.ToString(),
            asset.Condition.ToString(),
            asset.AssignedUserId,
            assignedName,
            asset.SupplierName,
            asset.InvoiceNumber,
            asset.PurchaseDate,
            asset.InServiceDate,
            asset.WarrantyExpiresAt,
            asset.PurchaseCost,
            asset.SalvageValue,
            asset.UsefulLifeMonths,
            asset.DepreciationMethod.ToString(),
            asset.EstimatedSellingValue,
            asset.DisposedAt,
            asset.ActualSoldPrice,
            asset.DisposalNotes,
            valuation,
            schedule,
            custodyDtos,
            maintenanceDtos,
            asset.CreatedAt,
            asset.UpdatedAt));
    }

    private static async Task<IResult> GetPublicAssetAsync(
        string token,
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var asset = await context.Assets
            .AsNoTracking()
            .Include(a => a.Category)
            .Include(a => a.AssignedUser)
            .FirstOrDefaultAsync(a => a.PublicToken == token && a.IsActive, cancellationToken);

        if (asset is null) return Results.NotFound();

        var now = DateTimeOffset.UtcNow;
        var isWarrantyActive = asset.WarrantyExpiresAt.HasValue && asset.WarrantyExpiresAt.Value >= now;
        var custodianName = asset.AssignedUser != null ? $"{asset.AssignedUser.FirstName} {asset.AssignedUser.LastName}".Trim() : null;

        return Results.Ok(new PublicAssetDto(
            asset.AssetTag,
            asset.Name,
            asset.Category?.Name,
            asset.CompanyName,
            asset.Department,
            asset.Location,
            asset.Brand,
            asset.Model,
            asset.SerialNumber,
            asset.Status.ToString(),
            asset.Condition.ToString(),
            custodianName,
            asset.PurchaseDate,
            asset.WarrantyExpiresAt,
            isWarrantyActive));
    }

    private static async Task<IResult> CreateAssetAsync(
        [FromBody] CreateAssetRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Asset name is required.");
        if (request.PurchaseCost < 0) return Results.BadRequest("Purchase cost cannot be negative.");

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var currentUserId = GetCurrentUserId(principal);

        string assetTag = request.AssetTag?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(assetTag))
        {
            var count = await context.Assets.CountAsync(a => a.ShopId == shopId, cancellationToken);
            assetTag = $"AST-{(count + 1):D4}";
        }

        if (await context.Assets.AnyAsync(a => a.ShopId == shopId && a.AssetTag == assetTag, cancellationToken))
        {
            return Results.BadRequest($"Asset tag '{assetTag}' already exists.");
        }

        var status = Enum.TryParse<AssetStatus>(request.Status, true, out var parsedStatus)
            ? parsedStatus
            : (request.AssignedUserId.HasValue ? AssetStatus.Active : AssetStatus.Available);

        var condition = Enum.TryParse<AssetCondition>(request.Condition, true, out var parsedCondition)
            ? parsedCondition
            : AssetCondition.New;

        var depMethod = Enum.TryParse<DepreciationMethod>(request.DepreciationMethod, true, out var parsedMethod)
            ? parsedMethod
            : DepreciationMethod.StraightLine;

        var purchaseDate = request.PurchaseDate ?? DateTimeOffset.UtcNow;
        var inServiceDate = request.InServiceDate ?? purchaseDate;
        var usefulLife = request.UsefulLifeMonths.GetValueOrDefault(36);
        if (usefulLife <= 0) usefulLife = 36;

        var asset = new Asset
        {
            ShopId = shopId,
            CategoryId = request.CategoryId,
            AssetTag = assetTag,
            PublicToken = Guid.NewGuid().ToString("N"),
            Name = request.Name.Trim(),
            Description = TrimOrNull(request.Description),
            CompanyName = TrimOrNull(request.CompanyName),
            Department = TrimOrNull(request.Department),
            Location = TrimOrNull(request.Location),
            Brand = TrimOrNull(request.Brand),
            Model = TrimOrNull(request.Model),
            SerialNumber = TrimOrNull(request.SerialNumber),
            Status = status,
            Condition = condition,
            AssignedUserId = request.AssignedUserId,
            AssignedAt = request.AssignedUserId.HasValue ? DateTimeOffset.UtcNow : null,
            SupplierName = TrimOrNull(request.SupplierName),
            InvoiceNumber = TrimOrNull(request.InvoiceNumber),
            PurchaseDate = purchaseDate,
            InServiceDate = inServiceDate,
            WarrantyExpiresAt = request.WarrantyExpiresAt,
            PurchaseCost = request.PurchaseCost,
            SalvageValue = Math.Max(0m, request.SalvageValue ?? 0m),
            UsefulLifeMonths = usefulLife,
            DepreciationMethod = depMethod,
            EstimatedSellingValue = request.EstimatedSellingValue ?? request.PurchaseCost,
            IsActive = true
        };

        context.Assets.Add(asset);
        await context.SaveChangesAsync(cancellationToken);

        // Record initial custody log if custodian or location is set
        if (asset.AssignedUserId.HasValue || !string.IsNullOrWhiteSpace(asset.Location))
        {
            context.AssetCustodyLogs.Add(new AssetCustodyLog
            {
                ShopId = shopId,
                AssetId = asset.Id,
                UserId = asset.AssignedUserId,
                ActionType = AssetCustodyActionType.CheckOut,
                Location = asset.Location,
                Condition = asset.Condition,
                Notes = "Initial assignment on asset creation",
                RecordedByUserId = currentUserId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        await activityLogger.LogAsync(
            "Asset",
            asset.Id,
            "AssetCreated",
            $"Asset created: {asset.AssetTag} ({asset.Name})",
            cancellationToken: cancellationToken);

        return Results.Created($"/api/v1/assets/{asset.Id}", new { asset.Id, asset.AssetTag });
    }

    private static async Task<IResult> UpdateAssetAsync(
        int id,
        [FromBody] UpdateAssetRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Asset name is required.");
        if (request.PurchaseCost < 0) return Results.BadRequest("Purchase cost cannot be negative.");

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == id && a.ShopId == shopId && a.IsActive, cancellationToken);
        if (asset is null) return Results.NotFound();

        if (!string.IsNullOrWhiteSpace(request.AssetTag) && request.AssetTag.Trim() != asset.AssetTag)
        {
            var newTag = request.AssetTag.Trim();
            if (await context.Assets.AnyAsync(a => a.ShopId == shopId && a.Id != id && a.AssetTag == newTag, cancellationToken))
            {
                return Results.BadRequest($"Asset tag '{newTag}' is already taken.");
            }
            asset.AssetTag = newTag;
        }

        if (Enum.TryParse<AssetStatus>(request.Status, true, out var parsedStatus))
        {
            asset.Status = parsedStatus;
        }
        if (Enum.TryParse<AssetCondition>(request.Condition, true, out var parsedCondition))
        {
            asset.Condition = parsedCondition;
        }
        if (Enum.TryParse<DepreciationMethod>(request.DepreciationMethod, true, out var parsedMethod))
        {
            asset.DepreciationMethod = parsedMethod;
        }

        asset.Name = request.Name.Trim();
        asset.CategoryId = request.CategoryId;
        asset.Description = TrimOrNull(request.Description);
        asset.CompanyName = TrimOrNull(request.CompanyName);
        asset.Department = TrimOrNull(request.Department);
        asset.Location = TrimOrNull(request.Location);
        asset.Brand = TrimOrNull(request.Brand);
        asset.Model = TrimOrNull(request.Model);
        asset.SerialNumber = TrimOrNull(request.SerialNumber);
        asset.SupplierName = TrimOrNull(request.SupplierName);
        asset.InvoiceNumber = TrimOrNull(request.InvoiceNumber);
        if (request.PurchaseDate.HasValue) asset.PurchaseDate = request.PurchaseDate.Value;
        if (request.InServiceDate.HasValue) asset.InServiceDate = request.InServiceDate.Value;
        asset.WarrantyExpiresAt = request.WarrantyExpiresAt;
        asset.PurchaseCost = request.PurchaseCost;
        asset.SalvageValue = Math.Max(0m, request.SalvageValue ?? 0m);
        if (request.UsefulLifeMonths.HasValue && request.UsefulLifeMonths.Value > 0)
        {
            asset.UsefulLifeMonths = request.UsefulLifeMonths.Value;
        }
        if (request.EstimatedSellingValue.HasValue)
        {
            asset.EstimatedSellingValue = request.EstimatedSellingValue.Value;
        }

        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "Asset",
            asset.Id,
            "AssetUpdated",
            $"Asset updated: {asset.AssetTag} ({asset.Name})",
            cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAssetAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == id && a.ShopId == shopId && a.IsActive, cancellationToken);
        if (asset is null) return Results.NotFound();

        asset.IsActive = false;
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "Asset",
            asset.Id,
            "AssetDeleted",
            $"Asset deleted: {asset.AssetTag} ({asset.Name})",
            cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> UpdateCustodyAsync(
        int id,
        [FromBody] UpdateCustodyRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var currentUserId = GetCurrentUserId(principal);
        var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == id && a.ShopId == shopId && a.IsActive, cancellationToken);
        if (asset is null) return Results.NotFound();

        if (!Enum.TryParse<AssetCustodyActionType>(request.ActionType, true, out var actionType))
        {
            return Results.BadRequest("Invalid custody action type. Allowed: CheckOut, CheckIn, Transfer.");
        }

        if (!string.IsNullOrWhiteSpace(request.Condition) && Enum.TryParse<AssetCondition>(request.Condition, true, out var cond))
        {
            asset.Condition = cond;
        }

        if (actionType == AssetCustodyActionType.CheckIn)
        {
            asset.AssignedUserId = null;
            asset.AssignedAt = null;
            asset.Status = AssetStatus.Available;
            if (!string.IsNullOrWhiteSpace(request.Location))
            {
                asset.Location = request.Location.Trim();
            }
        }
        else if (actionType == AssetCustodyActionType.CheckOut)
        {
            asset.AssignedUserId = request.UserId;
            asset.AssignedAt = DateTimeOffset.UtcNow;
            asset.Status = AssetStatus.Active;
            if (!string.IsNullOrWhiteSpace(request.Location))
            {
                asset.Location = request.Location.Trim();
            }
        }
        else // Transfer
        {
            if (request.UserId.HasValue) asset.AssignedUserId = request.UserId;
            if (!string.IsNullOrWhiteSpace(request.Location)) asset.Location = request.Location.Trim();
        }

        var log = new AssetCustodyLog
        {
            ShopId = shopId,
            AssetId = asset.Id,
            UserId = request.UserId ?? asset.AssignedUserId,
            ActionType = actionType,
            Location = request.Location ?? asset.Location,
            Condition = asset.Condition,
            Notes = TrimOrNull(request.Notes),
            RecordedByUserId = currentUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.AssetCustodyLogs.Add(log);
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "Asset",
            asset.Id,
            "AssetCustodyChanged",
            $"Asset {asset.AssetTag} custody action: {actionType}",
            cancellationToken: cancellationToken);

        return Results.Ok(new { success = true });
    }

    private static async Task<IResult> AddMaintenanceLogAsync(
        int id,
        [FromBody] AddMaintenanceLogRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return Results.BadRequest("Maintenance title is required.");
        if (request.Cost < 0) return Results.BadRequest("Cost cannot be negative.");

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var currentUserId = GetCurrentUserId(principal);
        var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == id && a.ShopId == shopId && a.IsActive, cancellationToken);
        if (asset is null) return Results.NotFound();

        if (!Enum.TryParse<AssetMaintenanceType>(request.MaintenanceType, true, out var mType))
        {
            mType = AssetMaintenanceType.Preventive;
        }

        if (request.SetUnderMaintenance == true)
        {
            asset.Status = AssetStatus.UnderMaintenance;
        }
        else if (asset.Status == AssetStatus.UnderMaintenance && request.SetUnderMaintenance == false)
        {
            asset.Status = asset.AssignedUserId.HasValue ? AssetStatus.Active : AssetStatus.Available;
        }

        var log = new AssetMaintenanceLog
        {
            ShopId = shopId,
            AssetId = asset.Id,
            ServiceTicketId = request.ServiceTicketId,
            MaintenanceType = mType,
            Title = request.Title.Trim(),
            Description = TrimOrNull(request.Description),
            Cost = request.Cost,
            PerformedBy = TrimOrNull(request.PerformedBy),
            PerformedAt = request.PerformedAt ?? DateTimeOffset.UtcNow,
            NextDueAt = request.NextDueAt,
            RecordedByUserId = currentUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.AssetMaintenanceLogs.Add(log);
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "Asset",
            asset.Id,
            "AssetMaintenanceLogged",
            $"Maintenance logged for {asset.AssetTag}: {log.Title} ({log.Cost:N0})",
            cancellationToken: cancellationToken);

        return Results.Created($"/api/v1/assets/{asset.Id}", new { log.Id });
    }

    private static async Task<IResult> DisposeAssetAsync(
        int id,
        [FromBody] DisposeAssetRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AssetStatus>(request.Status, true, out var status) ||
            (status != AssetStatus.Sold && status != AssetStatus.Retired && status != AssetStatus.WrittenOff))
        {
            return Results.BadRequest("Disposal status must be Sold, Retired, or WrittenOff.");
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == id && a.ShopId == shopId && a.IsActive, cancellationToken);
        if (asset is null) return Results.NotFound();

        asset.Status = status;
        asset.DisposedAt = request.DisposedAt ?? DateTimeOffset.UtcNow;
        asset.ActualSoldPrice = status == AssetStatus.Sold ? request.ActualSoldPrice : null;
        asset.DisposalNotes = TrimOrNull(request.DisposalNotes);
        asset.AssignedUserId = null;

        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "Asset",
            asset.Id,
            "AssetDisposed",
            $"Asset {asset.AssetTag} disposed as {status}",
            cancellationToken: cancellationToken);

        return Results.Ok(new { success = true });
    }

    private static async Task<IResult> ListCategoriesAsync(
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var categories = await context.AssetCategories
            .AsNoTracking()
            .Where(c => c.ShopId == shopId && c.IsActive)
            .Select(c => new AssetCategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.DefaultUsefulLifeMonths,
                c.DefaultSalvagePercent,
                c.IsActive,
                context.Assets.Count(a => a.CategoryId == c.Id && a.IsActive)))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return Results.Ok(categories);
    }

    private static async Task<IResult> CreateCategoryAsync(
        [FromBody] UpsertAssetCategoryRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Category name is required.");
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var name = request.Name.Trim();

        if (await context.AssetCategories.AnyAsync(c => c.ShopId == shopId && c.Name == name, cancellationToken))
        {
            return Results.BadRequest("Category with this name already exists.");
        }

        var category = new AssetCategory
        {
            ShopId = shopId,
            Name = name,
            Description = TrimOrNull(request.Description),
            DefaultUsefulLifeMonths = Math.Max(1, request.DefaultUsefulLifeMonths ?? 36),
            DefaultSalvagePercent = Math.Clamp(request.DefaultSalvagePercent ?? 0m, 0m, 100m),
            IsActive = request.IsActive ?? true
        };

        context.AssetCategories.Add(category);
        await context.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/assets/categories/{category.Id}", new { category.Id });
    }

    private static async Task<IResult> UpdateCategoryAsync(
        int id,
        [FromBody] UpsertAssetCategoryRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Category name is required.");
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var category = await context.AssetCategories.FirstOrDefaultAsync(c => c.Id == id && c.ShopId == shopId, cancellationToken);
        if (category is null) return Results.NotFound();

        var name = request.Name.Trim();
        if (await context.AssetCategories.AnyAsync(c => c.Id != id && c.ShopId == shopId && c.Name == name, cancellationToken))
        {
            return Results.BadRequest("Category with this name already exists.");
        }

        category.Name = name;
        category.Description = TrimOrNull(request.Description);
        if (request.DefaultUsefulLifeMonths.HasValue && request.DefaultUsefulLifeMonths.Value > 0)
        {
            category.DefaultUsefulLifeMonths = request.DefaultUsefulLifeMonths.Value;
        }
        if (request.DefaultSalvagePercent.HasValue)
        {
            category.DefaultSalvagePercent = Math.Clamp(request.DefaultSalvagePercent.Value, 0m, 100m);
        }
        if (request.IsActive.HasValue)
        {
            category.IsActive = request.IsActive.Value;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteCategoryAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var category = await context.AssetCategories.FirstOrDefaultAsync(c => c.Id == id && c.ShopId == shopId, cancellationToken);
        if (category is null) return Results.NotFound();

        category.IsActive = false;
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static AssetsSummaryDto CalculateSummary(IEnumerable<Asset> assets)
    {
        int total = 0;
        int active = 0;
        int available = 0;
        int maintenance = 0;
        int eol = 0;
        decimal purchaseCost = 0m;
        decimal bookValue = 0m;
        decimal accumDeprec = 0m;
        decimal monthlyDeprec = 0m;
        decimal sellingValue = 0m;
        decimal totalMaintenance = 0m;

        foreach (var a in assets)
        {
            total++;
            if (a.Status == AssetStatus.Active) active++;
            else if (a.Status == AssetStatus.Available) available++;
            else if (a.Status == AssetStatus.UnderMaintenance) maintenance++;

            var mCost = a.MaintenanceLogs.Sum(m => m.Cost);
            totalMaintenance += mCost;

            var v = AssetValuationCalculator.Calculate(a, mCost);
            purchaseCost += v.PurchaseCost;
            bookValue += v.CurrentBookValue;
            accumDeprec += v.AccumulatedDepreciation;
            if (a.Status == AssetStatus.Active || a.Status == AssetStatus.Available || a.Status == AssetStatus.UnderMaintenance)
            {
                monthlyDeprec += v.MonthlyDepreciation;
            }
            sellingValue += v.EffectiveSellingValue;

            if (v.IsEolApproaching || v.IsFullyDepreciated)
            {
                eol++;
            }
        }

        return new AssetsSummaryDto(
            total,
            active,
            available,
            maintenance,
            eol,
            purchaseCost,
            bookValue,
            accumDeprec,
            monthlyDeprec,
            sellingValue,
            totalMaintenance);
    }

    private static int? GetCurrentUserId(ClaimsPrincipal principal)
    {
        var val = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(val, out var parsed) ? parsed : null;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Csv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";
}
