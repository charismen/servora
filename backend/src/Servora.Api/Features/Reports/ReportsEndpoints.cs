using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Inventory;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Reports;

public static class ReportsEndpoints
{
    private const int MaxRangeDays = 370;

    public sealed record ReportSummaryDto(
        DateTimeOffset From,
        DateTimeOffset To,
        string GroupBy,
        bool CanViewCost,
        ReportTotalsDto Totals,
        IReadOnlyList<ServiceReportRowDto> ServiceRows,
        IReadOnlyList<PaymentReportRowDto> PaymentRows,
        IReadOnlyList<InventoryReportRowDto> InventoryRows,
        IReadOnlyList<InventoryMovementReportRowDto> InventoryMovementRows,
        IReadOnlyList<InventoryLowStockGroupDto> InventoryLowStockGroups,
        IReadOnlyList<InventoryValuationRowDto> InventoryValuationRows,
        IReadOnlyList<TicketAgingReportRowDto> AgingRows,
        IReadOnlyList<ApprovalReportRowDto> ApprovalRows);

    public sealed record ReportTotalsDto(
        int ReceivedTickets,
        int CompletedTickets,
        int CancelledTickets,
        int UnpaidTickets,
        int PartialPaidTickets,
        decimal TotalRevenue,
        decimal? TotalInventoryCost,
        decimal? TotalInventoryValue,
        decimal? GrossProfit,
        int LowStockItems);

    public sealed record ServiceReportRowDto(string Period, int Received, int Completed, int Cancelled);
    public sealed record PaymentReportRowDto(int TicketId, string TicketNumber, string CustomerName, string PaymentStatus, decimal AmountPaid, decimal? TotalAmount);
    public sealed record InventoryReportRowDto(int ItemId, string Sku, string Name, decimal OnHandQuantity, decimal ReservedQuantity, decimal AvailableQuantity, decimal ReorderLevel, decimal? CostPrice);
    public sealed record InventoryMovementReportRowDto(string Period, string Type, decimal Quantity, decimal? Cost);
    public sealed record InventoryLowStockGroupDto(string CategoryName, string ItemType, int ItemCount, decimal AvailableQuantity, decimal ReorderLevel);
    public sealed record InventoryValuationRowDto(string CategoryName, string ItemType, int ItemCount, decimal OnHandQuantity, decimal? InventoryValue);
    public sealed record TicketAgingReportRowDto(string Bucket, int Count);
    public sealed record ApprovalReportRowDto(string Status, int Count, decimal? EstimatedAmount);

    private sealed record Range(DateTimeOffset From, DateTimeOffset ToExclusive, string GroupBy);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/summary", GetSummaryAsync)
            .WithName("GetReportsSummary")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ReportsRead);

        app.MapGet("/reports/export", ExportAsync)
            .WithName("ExportReports")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ReportsRead);
    }

    private static async Task<IResult> GetSummaryAsync(
        string? from,
        string? to,
        string? groupBy,
        AppDbContext context,
        IShopContext shopContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var range = ParseRange(from, to, groupBy);
        if (range is null) return Results.BadRequest($"Invalid date range. Maximum range is {MaxRangeDays} days.");

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var canViewCost = await HasPermissionAsync(context, principal, shopId, ApplicationPermissions.InventoryCostRead, cancellationToken);
        var summary = await BuildSummaryAsync(context, shopId, range, canViewCost, cancellationToken);

        return Results.Ok(summary);
    }

    private static async Task<IResult> ExportAsync(
        string? from,
        string? to,
        string? groupBy,
        AppDbContext context,
        IShopContext shopContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var range = ParseRange(from, to, groupBy);
        if (range is null) return Results.BadRequest($"Invalid date range. Maximum range is {MaxRangeDays} days.");

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var canViewCost = await HasPermissionAsync(context, principal, shopId, ApplicationPermissions.InventoryCostRead, cancellationToken);
        var summary = await BuildSummaryAsync(context, shopId, range, canViewCost, cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(ToCsv(summary));

        return Results.File(bytes, "text/csv", $"reports-{summary.From:yyyyMMdd}-{summary.To:yyyyMMdd}.csv");
    }

    private static async Task<ReportSummaryDto> BuildSummaryAsync(AppDbContext context, int shopId, Range range, bool canViewCost, CancellationToken cancellationToken)
    {
        var tickets = await context.ServiceTickets
            .AsNoTracking()
            .Include(ticket => ticket.Status)
            .Where(ticket => ticket.ShopId == shopId && ticket.DeletedAt == null)
            .Where(ticket => ticket.CreatedAt >= range.From && ticket.CreatedAt < range.ToExclusive)
            .Select(ticket => new
            {
                ticket.Id,
                ticket.TicketNumber,
                ticket.CustomerNameSnapshot,
                ticket.PaymentStatus,
                ticket.AmountPaid,
                ticket.TotalAmount,
                ticket.CreatedAt,
                ticket.UpdatedAt,
                ticket.Status.IsFinal,
                ticket.Status.IsCancelled
            })
            .ToListAsync(cancellationToken);

        var closedTickets = await context.ServiceTickets
            .AsNoTracking()
            .Include(ticket => ticket.Status)
            .Where(ticket => ticket.ShopId == shopId && ticket.DeletedAt == null)
            .Where(ticket => ticket.UpdatedAt.HasValue && ticket.UpdatedAt.Value >= range.From && ticket.UpdatedAt.Value < range.ToExclusive)
            .Select(ticket => new
            {
                ticket.UpdatedAt,
                ticket.Status.IsFinal,
                ticket.Status.IsCancelled
            })
            .ToListAsync(cancellationToken);

        var payments = await context.PaymentRecords
            .AsNoTracking()
            .Where(payment =>
                payment.ShopId == shopId &&
                !payment.IsVoided &&
                payment.RecordedAt >= range.From &&
                payment.RecordedAt < range.ToExclusive)
            .Select(payment => new { payment.EntryType, payment.TransactionAmount })
            .ToListAsync(cancellationToken);

        var paymentRows = tickets
            .Where(ticket => ticket.PaymentStatus is PaymentStatus.Unpaid or PaymentStatus.Partial)
            .OrderBy(ticket => ticket.CreatedAt)
            .Select(ticket => new PaymentReportRowDto(
                ticket.Id,
                ticket.TicketNumber,
                ticket.CustomerNameSnapshot,
                ticket.PaymentStatus.ToString(),
                ticket.AmountPaid,
                ticket.TotalAmount))
            .ToList();

        var inventoryItems = await context.InventoryItems
            .AsNoTracking()
            .Include(item => item.Category)
            .Where(item => item.ShopId == shopId && item.IsActive)
            .ToListAsync(cancellationToken);

        var lowStockItems = inventoryItems
            .Where(item => item.OnHandQuantity - item.ReservedQuantity <= item.ReorderLevel)
            .ToList();

        var inventoryRows = lowStockItems
            .OrderBy(item => item.OnHandQuantity - item.ReservedQuantity)
            .ThenBy(item => item.Name)
            .Select(item => new InventoryReportRowDto(
                item.Id,
                item.Sku,
                item.Name,
                item.OnHandQuantity,
                item.ReservedQuantity,
                item.OnHandQuantity - item.ReservedQuantity,
                item.ReorderLevel,
                canViewCost ? item.CostPrice : null))
            .ToList();

        var inventoryLowStockGroups = lowStockItems
            .GroupBy(item => new { CategoryName = item.Category?.Name ?? "No category", item.ItemType })
            .OrderBy(group => group.Key.CategoryName)
            .ThenBy(group => group.Key.ItemType)
            .Select(group => new InventoryLowStockGroupDto(
                group.Key.CategoryName,
                group.Key.ItemType.ToString(),
                group.Count(),
                group.Sum(item => item.OnHandQuantity - item.ReservedQuantity),
                group.Sum(item => item.ReorderLevel)))
            .ToList();

        var inventoryValuationRows = inventoryItems
            .GroupBy(item => new { CategoryName = item.Category?.Name ?? "No category", item.ItemType })
            .OrderBy(group => group.Key.CategoryName)
            .ThenBy(group => group.Key.ItemType)
            .Select(group => new InventoryValuationRowDto(
                group.Key.CategoryName,
                group.Key.ItemType.ToString(),
                group.Count(),
                group.Sum(item => item.OnHandQuantity),
                canViewCost ? group.Sum(item => item.OnHandQuantity * item.CostPrice) : null))
            .ToList();

        var movements = await context.InventoryMovements
            .AsNoTracking()
            .Where(movement => movement.ShopId == shopId && movement.CreatedAt >= range.From && movement.CreatedAt < range.ToExclusive)
            .ToListAsync(cancellationToken);

        var inventoryMovementRows = movements
            .GroupBy(movement => new { Period = Bucket(movement.CreatedAt, range.GroupBy), movement.Type })
            .OrderBy(group => group.Key.Period)
            .ThenBy(group => group.Key.Type)
            .Select(group => new InventoryMovementReportRowDto(
                group.Key.Period,
                group.Key.Type.ToString(),
                group.Sum(movement => movement.Quantity),
                canViewCost ? group.Sum(movement => (movement.UnitCost ?? 0) * movement.Quantity) : null))
            .ToList();

        var servicePeriods = tickets
            .Select(ticket => Bucket(ticket.CreatedAt, range.GroupBy))
            .Concat(closedTickets.Where(ticket => ticket.UpdatedAt.HasValue).Select(ticket => Bucket(ticket.UpdatedAt!.Value, range.GroupBy)))
            .Distinct()
            .OrderBy(period => period);

        var serviceRows = servicePeriods
            .Select(period =>
            {
                var closed = closedTickets.Where(ticket => ticket.UpdatedAt.HasValue && Bucket(ticket.UpdatedAt.Value, range.GroupBy) == period).ToList();
                return new ServiceReportRowDto(
                    period,
                    tickets.Count(ticket => Bucket(ticket.CreatedAt, range.GroupBy) == period),
                    closed.Count(ticket => ticket.IsFinal && !ticket.IsCancelled),
                    closed.Count(ticket => ticket.IsCancelled));
            })
            .ToList();

        var agingRows = tickets
            .Where(ticket => !ticket.IsFinal && !ticket.IsCancelled)
            .GroupBy(ticket => AgingBucket((DateTimeOffset.UtcNow - ticket.CreatedAt).TotalDays))
            .Select(group => new TicketAgingReportRowDto(group.Key, group.Count()))
            .OrderBy(row => AgingSort(row.Bucket))
            .ToList();

        var approvals = await context.ApprovalRequests
            .AsNoTracking()
            .Where(approval => approval.ShopId == shopId && approval.CreatedAt >= range.From && approval.CreatedAt < range.ToExclusive)
            .ToListAsync(cancellationToken);

        var approvalRows = approvals
            .GroupBy(approval => approval.Status)
            .OrderBy(group => group.Key)
            .Select(group => new ApprovalReportRowDto(group.Key.ToString(), group.Count(), group.Sum(approval => approval.EstimatedAmount ?? 0)))
            .ToList();

        var itemCostById = inventoryItems.ToDictionary(item => item.Id, item => item.CostPrice);
        var totalRevenue = TicketBillingCalculator.RoundMoney(
            payments.Where(payment => payment.EntryType == PaymentEntryType.Payment).Sum(payment => payment.TransactionAmount) -
            payments.Where(payment => payment.EntryType == PaymentEntryType.Refund).Sum(payment => payment.TransactionAmount));
        var consumedCost = movements
            .Where(movement => movement.Type == InventoryMovementType.Consume)
            .Sum(movement => (movement.UnitCost ?? itemCostById.GetValueOrDefault(movement.InventoryItemId, 0m)) * movement.Quantity);
        var returnedCost = movements
            .Where(movement => movement.Type == InventoryMovementType.Return)
            .Sum(movement => (movement.UnitCost ?? itemCostById.GetValueOrDefault(movement.InventoryItemId, 0m)) * movement.Quantity);
        var totalInventoryCost = Math.Max(0m, consumedCost - returnedCost);
        var totalInventoryValue = inventoryItems.Sum(item => item.OnHandQuantity * item.CostPrice);

        return new ReportSummaryDto(
            range.From,
            range.ToExclusive.AddDays(-1),
            range.GroupBy,
            canViewCost,
            new ReportTotalsDto(
                tickets.Count,
                closedTickets.Count(ticket => ticket.IsFinal && !ticket.IsCancelled),
                closedTickets.Count(ticket => ticket.IsCancelled),
                tickets.Count(ticket => ticket.PaymentStatus == PaymentStatus.Unpaid),
                tickets.Count(ticket => ticket.PaymentStatus == PaymentStatus.Partial),
                totalRevenue,
                canViewCost ? totalInventoryCost : null,
                canViewCost ? totalInventoryValue : null,
                canViewCost ? totalRevenue - totalInventoryCost : null,
                inventoryRows.Count),
            serviceRows,
            paymentRows,
            inventoryRows,
            inventoryMovementRows,
            inventoryLowStockGroups,
            inventoryValuationRows,
            agingRows,
            approvalRows);
    }

    private static Range? ParseRange(string? from, string? to, string? groupBy)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var fromDate = string.IsNullOrWhiteSpace(from) ? today.AddDays(-30) : ParseDate(from);
        var toDate = string.IsNullOrWhiteSpace(to) ? today : ParseDate(to);
        if (fromDate is null || toDate is null || fromDate > toDate) return null;
        if ((toDate.Value - fromDate.Value).TotalDays > MaxRangeDays) return null;

        var group = (groupBy ?? "daily").Trim().ToLowerInvariant();
        if (group is not ("daily" or "weekly" or "monthly")) group = "daily";

        return new Range(
            new DateTimeOffset(fromDate.Value, TimeSpan.Zero),
            new DateTimeOffset(toDate.Value.AddDays(1), TimeSpan.Zero),
            group);
    }

    private static DateTime? ParseDate(string value)
    {
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.Date
            : null;
    }

    private static string Bucket(DateTimeOffset value, string groupBy)
    {
        var date = value.UtcDateTime.Date;
        if (groupBy == "monthly") return date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        if (groupBy == "weekly")
        {
            var offset = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string AgingBucket(double days)
    {
        if (days <= 1) return "0-1 days";
        if (days <= 3) return "2-3 days";
        if (days <= 7) return "4-7 days";
        if (days <= 14) return "8-14 days";
        return "15+ days";
    }

    private static int AgingSort(string bucket) => bucket switch
    {
        "0-1 days" => 1,
        "2-3 days" => 2,
        "4-7 days" => 3,
        "8-14 days" => 4,
        _ => 5
    };

    private static string ToCsv(ReportSummaryDto summary)
    {
        var lines = new List<string>
        {
            "Totals",
            Row("From", summary.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            Row("To", summary.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            Row("Received tickets", summary.Totals.ReceivedTickets),
            Row("Completed tickets", summary.Totals.CompletedTickets),
            Row("Cancelled tickets", summary.Totals.CancelledTickets),
            Row("Revenue", summary.Totals.TotalRevenue),
            Row("Inventory cost", summary.Totals.TotalInventoryCost),
            Row("Inventory value", summary.Totals.TotalInventoryValue),
            Row("Gross profit", summary.Totals.GrossProfit),
            "",
            "Service activity",
            Row("Period", "Received", "Completed", "Cancelled")
        };

        lines.AddRange(summary.ServiceRows.Select(row => Row(row.Period, row.Received, row.Completed, row.Cancelled)));
        lines.AddRange(["", "Payments", Row("Ticket", "Customer", "Status", "Paid", "Total")]);
        lines.AddRange(summary.PaymentRows.Select(row => Row(row.TicketNumber, row.CustomerName, row.PaymentStatus, row.AmountPaid, row.TotalAmount)));
        lines.AddRange(["", "Low stock", Row("SKU", "Name", "On hand", "Reserved", "Available", "Reorder", "Cost")]);
        lines.AddRange(summary.InventoryRows.Select(row => Row(row.Sku, row.Name, row.OnHandQuantity, row.ReservedQuantity, row.AvailableQuantity, row.ReorderLevel, row.CostPrice)));
        lines.AddRange(["", "Low stock by category/type", Row("Category", "Type", "Items", "Available", "Reorder")]);
        lines.AddRange(summary.InventoryLowStockGroups.Select(row => Row(row.CategoryName, row.ItemType, row.ItemCount, row.AvailableQuantity, row.ReorderLevel)));
        lines.AddRange(["", "Inventory valuation", Row("Category", "Type", "Items", "On hand", "Value")]);
        lines.AddRange(summary.InventoryValuationRows.Select(row => Row(row.CategoryName, row.ItemType, row.ItemCount, row.OnHandQuantity, row.InventoryValue)));
        lines.AddRange(["", "Inventory movement", Row("Period", "Type", "Quantity", "Cost")]);
        lines.AddRange(summary.InventoryMovementRows.Select(row => Row(row.Period, row.Type, row.Quantity, row.Cost)));
        lines.AddRange(["", "Ticket aging", Row("Bucket", "Count")]);
        lines.AddRange(summary.AgingRows.Select(row => Row(row.Bucket, row.Count)));
        lines.AddRange(["", "Approvals", Row("Status", "Count", "Estimated amount")]);
        lines.AddRange(summary.ApprovalRows.Select(row => Row(row.Status, row.Count, row.EstimatedAmount)));

        return string.Join(Environment.NewLine, lines);
    }

    private static string Row(params object?[] values)
    {
        return string.Join(",", values.Select(value => Escape(value?.ToString() ?? string.Empty)));
    }

    private static string Escape(string value)
    {
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static async Task<bool> HasPermissionAsync(AppDbContext context, ClaimsPrincipal principal, int shopId, string permission, CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId)) return false;

        var roleIds = context.ShopUserRoles
            .Where(userRole => userRole.ShopUser.UserId == userId && userRole.ShopUser.ShopId == shopId && userRole.ShopUser.Status == ShopUserStatus.Active)
            .Select(userRole => userRole.RoleId);

        return await context.RolePermissions.AnyAsync(rolePermission => roleIds.Contains(rolePermission.RoleId) && rolePermission.Permission.Code == permission, cancellationToken);
    }
}
