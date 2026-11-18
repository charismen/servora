using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Attendance;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Settings;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Payroll;

public static class PayrollEndpoints
{
    public sealed record CreatePayrollAdjustmentRequest(
        int UserId,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        decimal Amount,
        string Reason);

    public sealed record WorkerPayrollSlipDto(
        string ShopName,
        string ShopAddress,
        string ShopPhone,
        string CurrencyCode,
        DateOnly From,
        DateOnly To,
        string Cycle,
        WorkerPayrollRowDto Worker,
        IReadOnlyList<AttendanceEndpoints.AttendanceItemDto> Attendances);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/payroll/summary", GetPayrollSummaryAsync)
            .WithName("GetPayrollSummary")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.PayrollRead);

        app.MapPost("/payroll/adjustments", CreatePayrollAdjustmentAsync)
            .WithName("CreatePayrollAdjustment")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.PayrollManage);

        app.MapDelete("/payroll/adjustments/{id:int}", DeletePayrollAdjustmentAsync)
            .WithName("DeletePayrollAdjustment")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.PayrollManage);

        app.MapGet("/payroll/export", ExportPayrollCsvAsync)
            .WithName("ExportPayrollCsv")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.PayrollRead);

        app.MapGet("/payroll/slip/{userId:int}", GetPayrollSlipAsync)
            .WithName("GetPayrollSlip")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.PayrollRead);
    }

    private static async Task<IResult> GetPayrollSummaryAsync(
        string? from,
        string? to,
        string? cycle,
        int? roleId,
        int? userId,
        bool? includeFullTime,
        AppDbContext context,
        IShopContext shopContext,
        PayrollCalculationService payrollService,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var (fromDate, toDate, normalizedCycle) = ResolvePeriod(from, to, cycle);
        if (toDate < fromDate)
        {
            return Results.BadRequest("End date must be on or after start date.");
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var canManage = await HasPermissionAsync(context, principal, shopId, ApplicationPermissions.PayrollManage, cancellationToken);
        var effectiveUserId = canManage ? userId : GetCurrentUserId(principal);
        if (!canManage && !effectiveUserId.HasValue)
        {
            return Results.Forbid();
        }

        var summary = await payrollService.CalculateSummaryAsync(
            shopId,
            fromDate,
            toDate,
            normalizedCycle,
            canManage ? roleId : null,
            effectiveUserId,
            includeFullTime ?? false,
            cancellationToken);

        return Results.Ok(summary);
    }

    private static async Task<IResult> CreatePayrollAdjustmentAsync(
        CreatePayrollAdjustmentRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (request.Amount == 0m)
        {
            return Results.BadRequest("Adjustment amount must be non-zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest("Adjustment reason is required.");
        }

        if (request.PeriodEnd < request.PeriodStart)
        {
            return Results.BadRequest("Period end date must be on or after period start date.");
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var shopUser = await context.ShopUsers
            .FirstOrDefaultAsync(item => item.ShopId == shopId && item.UserId == request.UserId, cancellationToken);
        if (shopUser is null)
        {
            return Results.BadRequest("Worker was not found in this shop.");
        }

        var actorUserId = GetCurrentUserId(principal) ?? request.UserId;
        var adjustment = new PayrollAdjustment
        {
            ShopId = shopId,
            ShopUserId = shopUser.Id,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            Reason = request.Reason.Trim(),
            CreatedByUserId = actorUserId
        };

        context.PayrollAdjustments.Add(adjustment);
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "PayrollAdjustment",
            adjustment.Id,
            "PayrollAdjustmentCreated",
            $"Added payroll adjustment ({adjustment.Amount:0.##}) for user #{request.UserId}: {adjustment.Reason}",
            cancellationToken: cancellationToken);

        return Results.Created($"/api/v1/payroll/adjustments/{adjustment.Id}", new { adjustment.Id });
    }

    private static async Task<IResult> DeletePayrollAdjustmentAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var adjustment = await context.PayrollAdjustments
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (adjustment is null)
        {
            return Results.NotFound();
        }

        context.PayrollAdjustments.Remove(adjustment);
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "PayrollAdjustment",
            id,
            "PayrollAdjustmentDeleted",
            $"Deleted payroll adjustment #{id}.",
            cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> ExportPayrollCsvAsync(
        string? from,
        string? to,
        string? cycle,
        int? roleId,
        int? userId,
        bool? includeFullTime,
        IShopContext shopContext,
        PayrollCalculationService payrollService,
        CancellationToken cancellationToken)
    {
        var (fromDate, toDate, normalizedCycle) = ResolvePeriod(from, to, cycle);
        if (toDate < fromDate)
        {
            return Results.BadRequest("End date must be on or after start date.");
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var summary = await payrollService.CalculateSummaryAsync(
            shopId,
            fromDate,
            toDate,
            normalizedCycle,
            roleId,
            userId,
            includeFullTime ?? false,
            cancellationToken);

        var csv = BuildCsv(summary);
        var bytes = Encoding.UTF8.GetBytes(csv);
        return Results.File(bytes, "text/csv", $"payroll-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv");
    }

    private static async Task<IResult> GetPayrollSlipAsync(
        int userId,
        string? from,
        string? to,
        string? cycle,
        AppDbContext context,
        IShopContext shopContext,
        PayrollCalculationService payrollService,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var (fromDate, toDate, normalizedCycle) = ResolvePeriod(from, to, cycle);
        if (toDate < fromDate)
        {
            return Results.BadRequest("End date must be on or after start date.");
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var canManage = await HasPermissionAsync(context, principal, shopId, ApplicationPermissions.PayrollManage, cancellationToken);
        if (!canManage && GetCurrentUserId(principal) != userId)
        {
            return Results.Forbid();
        }
        var summary = await payrollService.CalculateSummaryAsync(
            shopId,
            fromDate,
            toDate,
            normalizedCycle,
            null,
            userId,
            includeFullTime: true,
            cancellationToken);

        var workerRow = summary.Workers.FirstOrDefault();
        if (workerRow is null)
        {
            return Results.NotFound();
        }

        var shopSettings = await GetShopProfileSettings.ReadSettingsAsync(context, shopContext, cancellationToken);
        var shopTimeZone = ShopTimeZoneHelper.ResolveTimeZone(shopSettings.OperationalTimeZone);
        var attendances = await context.Attendances
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.ApprovedByUser)
            .Include(item => item.ShopUser)
                .ThenInclude(su => su.Roles)
                    .ThenInclude(r => r.Role)
            .Where(item =>
                item.ShopId == shopId &&
                item.UserId == userId &&
                item.WorkDate >= fromDate &&
                item.WorkDate <= toDate &&
                item.Status == AttendanceStatus.Approved)
            .OrderBy(item => item.WorkDate)
            .ThenBy(item => item.CheckInAt)
            .ToListAsync(cancellationToken);

        var attendanceDtos = attendances.Select(record =>
        {
            var workerName = $"{record.User.FirstName} {record.User.LastName}".Trim();
            var workerEmail = record.User.Email ?? string.Empty;
            var approvedByName = record.ApprovedByUser is null
                ? null
                : $"{record.ApprovedByUser.FirstName} {record.ApprovedByUser.LastName}".Trim();
            var roles = record.ShopUser.Roles
                .OrderBy(role => role.Role.Name)
                .Select(role => role.Role.Name ?? string.Empty)
                .ToList();

            var localCheckInAt = TimeZoneInfo.ConvertTime(record.CheckInAt, shopTimeZone);
            var localCheckOutAt = record.CheckOutAt.HasValue
                ? TimeZoneInfo.ConvertTime(record.CheckOutAt.Value, shopTimeZone)
                : (DateTimeOffset?)null;

            return new AttendanceEndpoints.AttendanceItemDto(
                record.Id,
                record.ShopUserId,
                record.UserId,
                string.IsNullOrWhiteSpace(workerName) ? workerEmail : workerName,
                workerEmail,
                record.ShopUser.EmploymentType.ToString(),
                roles,
                record.WorkDate,
                localCheckInAt,
                localCheckOutAt,
                record.BreakMinutes,
                record.TotalHours,
                record.IsAdminOverride,
                record.OverrideReason,
                record.Notes,
                record.Status.ToString(),
                record.ApprovedByUserId,
                string.IsNullOrWhiteSpace(approvedByName) ? record.ApprovedByUser?.Email : approvedByName,
                record.ApprovedAt);
        }).ToList();

        return Results.Ok(new WorkerPayrollSlipDto(
            shopSettings.ShopName,
            shopSettings.Address,
            shopSettings.PhoneNumber,
            shopSettings.CurrencyCode,
            fromDate,
            toDate,
            normalizedCycle,
            workerRow,
            attendanceDtos));
    }

    private static (DateOnly From, DateOnly To, string Cycle) ResolvePeriod(string? from, string? to, string? cycle)
    {
        var normalizedCycle = string.Equals(cycle?.Trim(), "weekly", StringComparison.OrdinalIgnoreCase)
            ? "weekly"
            : "monthly";

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = ParseDateOnly(from);
        var toDate = ParseDateOnly(to);

        if (fromDate.HasValue && toDate.HasValue)
        {
            return (fromDate.Value, toDate.Value, normalizedCycle);
        }

        if (normalizedCycle == "weekly")
        {
            var diff = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var weekStart = today.AddDays(-diff);
            return (weekStart, weekStart.AddDays(6), normalizedCycle);
        }

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        return (monthStart, monthStart.AddMonths(1).AddDays(-1), normalizedCycle);
    }

    private static DateOnly? ParseDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var general)
            ? general
            : null;
    }

    private static int? GetCurrentUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private static async Task<bool> HasPermissionAsync(
        AppDbContext context,
        ClaimsPrincipal principal,
        int shopId,
        string permission,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId(principal);
        if (!userId.HasValue) return false;

        var roleIds = context.ShopUserRoles
            .Where(userRole => userRole.ShopUser.UserId == userId.Value && userRole.ShopUser.ShopId == shopId && userRole.ShopUser.Status == Servora.Api.Features.Shops.ShopUserStatus.Active)
            .Select(userRole => userRole.RoleId);

        return await context.RolePermissions.AnyAsync(rolePermission => roleIds.Contains(rolePermission.RoleId) && rolePermission.Permission.Code == permission, cancellationToken);
    }

    private static string BuildCsv(PayrollSummaryDto summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PeriodStart,PeriodEnd,Cycle,WorkerName,Email,Roles,EmploymentType,BasePayMode,DaysWorked,HoursWorked,BaseCalculatedWage,TierBreakdown,BonusTotal,BonusBreakdown,ManualAdjustments,FinalPayableAmount");

        foreach (var worker in summary.Workers)
        {
            var tierSummary = string.Join(" | ", worker.TierBreakdown.Select(t => $"{t.TierLabel}: {t.ShiftCount} shifts ({t.TotalHours:0.##}h) = {t.SubtotalAmount:0.##}"));
            var bonusSummary = string.Join(" | ", worker.BonusBreakdown.Select(b => $"{b.BonusName}: {b.FormulaSummary} = {b.TotalBonusAmount:0.##}"));

            sb.Append(EscapeCsv(summary.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(',')
              .Append(EscapeCsv(summary.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(',')
              .Append(EscapeCsv(summary.Cycle)).Append(',')
              .Append(EscapeCsv(worker.FullName)).Append(',')
              .Append(EscapeCsv(worker.Email)).Append(',')
              .Append(EscapeCsv(string.Join("; ", worker.Roles))).Append(',')
              .Append(EscapeCsv(worker.EmploymentType)).Append(',')
              .Append(EscapeCsv(worker.BasePayMode)).Append(',')
              .Append(worker.TotalDaysWorked.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(worker.TotalHoursWorked.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
              .Append(worker.BaseCalculatedWage.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
              .Append(EscapeCsv(tierSummary)).Append(',')
              .Append(worker.TotalBonusAmount.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
              .Append(EscapeCsv(bonusSummary)).Append(',')
              .Append(worker.TotalAdjustments.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
              .Append(worker.FinalPayableAmount.ToString("0.##", CultureInfo.InvariantCulture))
              .AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
        return value;
    }
}

