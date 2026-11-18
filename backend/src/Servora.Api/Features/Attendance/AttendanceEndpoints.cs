using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Settings;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Attendance;

public static class AttendanceEndpoints
{
    public sealed record AttendanceItemDto(
        int Id,
        int ShopUserId,
        int UserId,
        string WorkerName,
        string WorkerEmail,
        string EmploymentType,
        IReadOnlyList<string> Roles,
        DateOnly WorkDate,
        DateTimeOffset CheckInAt,
        DateTimeOffset? CheckOutAt,
        int BreakMinutes,
        decimal TotalHours,
        bool IsAdminOverride,
        string? OverrideReason,
        string? Notes,
        string Status,
        int? ApprovedByUserId,
        string? ApprovedByName,
        DateTimeOffset? ApprovedAt);

    public sealed record AttendanceListSummaryDto(
        int TotalEntries,
        int PendingEntries,
        int ApprovedEntries,
        int RejectedEntries,
        decimal TotalApprovedHours,
        string OperationalOpenTime,
        string OperationalCloseTime,
        int DefaultBreakMinutes,
        string OperationalTimeZone);

    public sealed record AttendanceListResponseDto(
        DateOnly From,
        DateOnly To,
        AttendanceListSummaryDto Summary,
        IReadOnlyList<AttendanceItemDto> Items);

    public sealed record UpsertAttendanceRequest(
        int UserId,
        DateOnly WorkDate,
        DateTimeOffset CheckInAt,
        DateTimeOffset? CheckOutAt,
        int BreakMinutes = 60,
        bool IsAdminOverride = false,
        string? OverrideReason = null,
        string? Notes = null,
        bool ApproveImmediately = false,
        string? CheckInTime = null,
        string? CheckOutTime = null);

    public sealed record BulkApproveAttendanceRequest(
        IReadOnlyCollection<int> AttendanceIds,
        string Status = "Approved");

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/attendances", ListAttendancesAsync)
            .WithName("GetAttendances")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AttendanceRead);

        app.MapPost("/attendances", CreateAttendanceAsync)
            .WithName("CreateAttendance")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AttendanceWrite);

        app.MapPut("/attendances/{id:int}", UpdateAttendanceAsync)
            .WithName("UpdateAttendance")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AttendanceWrite);

        app.MapPost("/attendances/approve-bulk", BulkApproveAttendancesAsync)
            .WithName("BulkApproveAttendances")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AttendanceApprove);

        app.MapDelete("/attendances/{id:int}", DeleteAttendanceAsync)
            .WithName("DeleteAttendance")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.AttendanceWrite);
    }

    private static async Task<IResult> ListAttendancesAsync(
        string? from,
        string? to,
        int? userId,
        int? roleId,
        string? status,
        string? employmentType,
        AppDbContext context,
        IShopContext shopContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = ParseDateOnly(from) ?? new DateOnly(today.Year, today.Month, 1);
        var toDate = ParseDateOnly(to) ?? fromDate.AddMonths(1).AddDays(-1);
        if (toDate < fromDate)
        {
            return Results.BadRequest("End date must be on or after start date.");
        }

        var actorUserId = GetCurrentUserId(principal);
        if (!actorUserId.HasValue)
        {
            return Results.Unauthorized();
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var shopSettings = await GetShopProfileSettings.ReadSettingsAsync(context, shopContext, cancellationToken);

        var canManage = await HasPermissionAsync(context, principal, shopId, ApplicationPermissions.AttendanceWrite, cancellationToken)
            || await HasPermissionAsync(context, principal, shopId, ApplicationPermissions.AttendanceApprove, cancellationToken);

        var query = context.Attendances
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.ApprovedByUser)
            .Include(item => item.ShopUser)
                .ThenInclude(shopUser => shopUser.Roles)
                    .ThenInclude(role => role.Role)
            .Where(item => item.ShopId == shopId && item.WorkDate >= fromDate && item.WorkDate <= toDate);

        if (!canManage)
        {
            // Non-managers can strictly view only their own attendance records
            query = query.Where(item => item.UserId == actorUserId.Value);
        }
        else
        {
            if (userId.HasValue)
            {
                query = query.Where(item => item.UserId == userId.Value);
            }

            if (roleId.HasValue)
            {
                query = query.Where(item => item.ShopUser.Roles.Any(role => role.RoleId == roleId.Value));
            }

            if (!string.IsNullOrWhiteSpace(employmentType) && Enum.TryParse<EmploymentType>(employmentType.Trim(), true, out var parsedEmploymentType))
            {
                query = query.Where(item => item.ShopUser.EmploymentType == parsedEmploymentType);
            }
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AttendanceStatus>(status.Trim(), true, out var parsedStatus))
        {
            query = query.Where(item => item.Status == parsedStatus);
        }

        var records = await query
            .OrderByDescending(item => item.WorkDate)
            .ThenByDescending(item => item.CheckInAt)
            .ToListAsync(cancellationToken);

        var shopTimeZone = ShopTimeZoneHelper.ResolveTimeZone(shopSettings.OperationalTimeZone);

        var items = records.Select(record =>
        {
            var workerName = $"{record.User.FirstName} {record.User.LastName}".Trim();
            var workerEmail = record.User.Email ?? string.Empty;
            var approvedByName = record.ApprovedByUser is null
                ? null
                : $"{record.ApprovedByUser.FirstName} {record.ApprovedByUser.LastName}".Trim();
            var roles = record.ShopUser.Roles
                .OrderBy(role => role.Role.Name)
                .Select(role => role.Role.Name ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            var localCheckInAt = TimeZoneInfo.ConvertTime(record.CheckInAt, shopTimeZone);
            var localCheckOutAt = record.CheckOutAt.HasValue
                ? TimeZoneInfo.ConvertTime(record.CheckOutAt.Value, shopTimeZone)
                : (DateTimeOffset?)null;

            return new AttendanceItemDto(
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

        var summary = new AttendanceListSummaryDto(
            items.Count,
            items.Count(item => item.Status == nameof(AttendanceStatus.Pending)),
            items.Count(item => item.Status == nameof(AttendanceStatus.Approved)),
            items.Count(item => item.Status == nameof(AttendanceStatus.Rejected)),
            Math.Round(items.Where(item => item.Status == nameof(AttendanceStatus.Approved)).Sum(item => item.TotalHours), 2, MidpointRounding.AwayFromZero),
            shopSettings.OperationalOpenTime,
            shopSettings.OperationalCloseTime,
            shopSettings.OperationalDefaultBreakMinutes,
            shopSettings.OperationalTimeZone);

        return Results.Ok(new AttendanceListResponseDto(fromDate, toDate, summary, items));
    }

    private static async Task<IResult> CreateAttendanceAsync(
        UpsertAttendanceRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var shopUser = await context.ShopUsers
            .FirstOrDefaultAsync(item => item.ShopId == shopId && item.UserId == request.UserId, cancellationToken);
        if (shopUser is null)
        {
            return Results.BadRequest("Selected worker was not found in this shop.");
        }

        var shopSettings = await GetShopProfileSettings.ReadSettingsAsync(context, shopContext, cancellationToken);
        var shopTimeZone = ShopTimeZoneHelper.ResolveTimeZone(shopSettings.OperationalTimeZone);

        var inTime = !string.IsNullOrWhiteSpace(request.CheckInTime) && TimeOnly.TryParseExact(request.CheckInTime.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedIn)
            ? parsedIn
            : TimeOnly.FromTimeSpan(request.CheckInAt.TimeOfDay);

        TimeOnly? outTime = null;
        if (!string.IsNullOrWhiteSpace(request.CheckOutTime) && TimeOnly.TryParseExact(request.CheckOutTime.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedOut))
        {
            outTime = parsedOut;
        }
        else if (request.CheckOutAt.HasValue)
        {
            outTime = TimeOnly.FromTimeSpan(request.CheckOutAt.Value.TimeOfDay);
        }

        var effectiveCheckInAt = ShopTimeZoneHelper.CreateShopDateTimeOffset(request.WorkDate, inTime, shopTimeZone);
        var effectiveCheckOutAt = outTime.HasValue
            ? ShopTimeZoneHelper.CreateShopDateTimeOffset(request.WorkDate, outTime.Value, shopTimeZone)
            : (DateTimeOffset?)null;

        var validation = await AttendanceCalculator.ValidateAndCalculateAsync(
            context,
            shopId,
            shopUser.Id,
            request.WorkDate,
            effectiveCheckInAt,
            effectiveCheckOutAt,
            request.BreakMinutes,
            request.IsAdminOverride,
            request.OverrideReason,
            shopSettings.OperationalOpenTime,
            shopSettings.OperationalCloseTime,
            null,
            shopTimeZone,
            cancellationToken);

        if (!validation.IsValid)
        {
            return Results.BadRequest(validation.ErrorMessage ?? "Invalid attendance entry.");
        }

        var actorUserId = GetCurrentUserId(principal);
        var canApprove = await HasPermissionAsync(context, principal, shopId, ApplicationPermissions.AttendanceApprove, cancellationToken);
        var isApproved = request.ApproveImmediately && canApprove;

        var record = new AttendanceRecord
        {
            ShopId = shopId,
            ShopUserId = shopUser.Id,
            UserId = shopUser.UserId,
            WorkDate = request.WorkDate,
            CheckInAt = effectiveCheckInAt,
            CheckOutAt = effectiveCheckOutAt,
            BreakMinutes = Math.Max(0, request.BreakMinutes),
            TotalHours = validation.TotalHours,
            IsAdminOverride = validation.IsOutsideOperationalHours || request.IsAdminOverride,
            OverrideReason = string.IsNullOrWhiteSpace(request.OverrideReason) ? null : request.OverrideReason.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = isApproved ? AttendanceStatus.Approved : AttendanceStatus.Pending,
            ApprovedByUserId = isApproved ? actorUserId : null,
            ApprovedAt = isApproved ? DateTimeOffset.UtcNow : null
        };

        context.Attendances.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "Attendance",
            record.Id,
            "AttendanceCreated",
            $"Attendance logged for user #{shopUser.UserId} on {request.WorkDate:yyyy-MM-dd} ({validation.TotalHours:0.##}h).",
            cancellationToken: cancellationToken);

        return Results.Created($"/api/v1/attendances/{record.Id}", new { record.Id, record.TotalHours, status = record.Status.ToString() });
    }

    private static async Task<IResult> UpdateAttendanceAsync(
        int id,
        UpsertAttendanceRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var record = await context.Attendances
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (record is null)
        {
            return Results.NotFound();
        }

        var shopUser = await context.ShopUsers
            .FirstOrDefaultAsync(item => item.ShopId == shopId && item.UserId == request.UserId, cancellationToken);
        if (shopUser is null)
        {
            return Results.BadRequest("Selected worker was not found in this shop.");
        }

        var shopSettings = await GetShopProfileSettings.ReadSettingsAsync(context, shopContext, cancellationToken);
        var shopTimeZone = ShopTimeZoneHelper.ResolveTimeZone(shopSettings.OperationalTimeZone);

        var inTime = !string.IsNullOrWhiteSpace(request.CheckInTime) && TimeOnly.TryParseExact(request.CheckInTime.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedIn)
            ? parsedIn
            : TimeOnly.FromTimeSpan(request.CheckInAt.TimeOfDay);

        TimeOnly? outTime = null;
        if (!string.IsNullOrWhiteSpace(request.CheckOutTime) && TimeOnly.TryParseExact(request.CheckOutTime.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedOut))
        {
            outTime = parsedOut;
        }
        else if (request.CheckOutAt.HasValue)
        {
            outTime = TimeOnly.FromTimeSpan(request.CheckOutAt.Value.TimeOfDay);
        }

        var effectiveCheckInAt = ShopTimeZoneHelper.CreateShopDateTimeOffset(request.WorkDate, inTime, shopTimeZone);
        var effectiveCheckOutAt = outTime.HasValue
            ? ShopTimeZoneHelper.CreateShopDateTimeOffset(request.WorkDate, outTime.Value, shopTimeZone)
            : (DateTimeOffset?)null;

        var validation = await AttendanceCalculator.ValidateAndCalculateAsync(
            context,
            shopId,
            shopUser.Id,
            request.WorkDate,
            effectiveCheckInAt,
            effectiveCheckOutAt,
            request.BreakMinutes,
            request.IsAdminOverride,
            request.OverrideReason,
            shopSettings.OperationalOpenTime,
            shopSettings.OperationalCloseTime,
            record.Id,
            shopTimeZone,
            cancellationToken);

        if (!validation.IsValid)
        {
            return Results.BadRequest(validation.ErrorMessage ?? "Invalid attendance entry.");
        }

        var actorUserId = GetCurrentUserId(principal);

        record.ShopUserId = shopUser.Id;
        record.UserId = shopUser.UserId;
        record.WorkDate = request.WorkDate;
        record.CheckInAt = effectiveCheckInAt;
        record.CheckOutAt = effectiveCheckOutAt;
        record.BreakMinutes = Math.Max(0, request.BreakMinutes);
        record.TotalHours = validation.TotalHours;
        record.IsAdminOverride = validation.IsOutsideOperationalHours || request.IsAdminOverride;
        record.OverrideReason = string.IsNullOrWhiteSpace(request.OverrideReason) ? null : request.OverrideReason.Trim();
        record.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        if (request.ApproveImmediately && record.Status != AttendanceStatus.Approved)
        {
            var canApprove = await HasPermissionAsync(context, principal, shopId, ApplicationPermissions.AttendanceApprove, cancellationToken);
            if (canApprove)
            {
                record.Status = AttendanceStatus.Approved;
                record.ApprovedByUserId = actorUserId;
                record.ApprovedAt = DateTimeOffset.UtcNow;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync(
            "Attendance",
            record.Id,
            "AttendanceUpdated",
            $"Attendance #{record.Id} updated ({validation.TotalHours:0.##}h).",
            cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> BulkApproveAttendancesAsync(
        BulkApproveAttendanceRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var ids = (request.AttendanceIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            return Results.BadRequest("Select at least one attendance entry.");
        }

        var nextStatus = Enum.TryParse<AttendanceStatus>(request.Status, true, out var parsed)
            ? parsed
            : AttendanceStatus.Approved;

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var actorUserId = GetCurrentUserId(principal);

        var records = await context.Attendances
            .Where(item => item.ShopId == shopId && ids.Contains(item.Id))
            .ToListAsync(cancellationToken);

        if (records.Count == 0)
        {
            return Results.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var record in records)
        {
            record.Status = nextStatus;
            record.ApprovedByUserId = actorUserId;
            record.ApprovedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync(
            "Attendance",
            shopId,
            $"AttendanceBulk{nextStatus}",
            $"Updated {records.Count} attendance entries to {nextStatus}.",
            cancellationToken: cancellationToken);

        return Results.Ok(new { updatedCount = records.Count, status = nextStatus.ToString() });
    }

    private static async Task<IResult> DeleteAttendanceAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var record = await context.Attendances
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (record is null)
        {
            return Results.NotFound();
        }

        context.Attendances.Remove(record);
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "Attendance",
            id,
            "AttendanceDeleted",
            $"Deleted attendance entry #{id}.",
            cancellationToken: cancellationToken);

        return Results.NoContent();
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

    private static async Task<bool> HasPermissionAsync(AppDbContext context, ClaimsPrincipal principal, int shopId, string permission, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId(principal);
        if (!userId.HasValue) return false;

        var roleIds = context.ShopUserRoles
            .Where(userRole => userRole.ShopUser.UserId == userId.Value && userRole.ShopUser.ShopId == shopId && userRole.ShopUser.Status == ShopUserStatus.Active)
            .Select(userRole => userRole.RoleId);

        return await context.RolePermissions.AnyAsync(rolePermission => roleIds.Contains(rolePermission.RoleId) && rolePermission.Permission.Code == permission, cancellationToken);
    }
}

