using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Attendance;

public sealed record AttendanceValidationResult(
    bool IsValid,
    decimal TotalHours,
    bool IsOutsideOperationalHours,
    string? ErrorMessage = null);

public static class AttendanceCalculator
{
    public static decimal CalculateHours(DateTimeOffset checkInAt, DateTimeOffset? checkOutAt, int breakMinutes)
    {
        if (checkOutAt is null || checkOutAt.Value <= checkInAt)
        {
            return 0m;
        }

        var rawMinutes = (checkOutAt.Value - checkInAt).TotalMinutes - Math.Max(0, breakMinutes);
        if (rawMinutes <= 0)
        {
            return 0m;
        }

        return Math.Round((decimal)(rawMinutes / 60.0), 2, MidpointRounding.AwayFromZero);
    }

    public static bool IsOutsideOperatingWindow(
        DateTimeOffset checkInAt,
        DateTimeOffset? checkOutAt,
        string? openTimeStr,
        string? closeTimeStr,
        TimeZoneInfo? shopTimeZone = null)
    {
        if (!TimeOnly.TryParseExact(openTimeStr?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var openTime))
        {
            openTime = new TimeOnly(9, 0);
        }

        if (!TimeOnly.TryParseExact(closeTimeStr?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var closeTime))
        {
            closeTime = new TimeOnly(18, 0);
        }

        var effectiveCheckIn = shopTimeZone is not null ? TimeZoneInfo.ConvertTime(checkInAt, shopTimeZone) : checkInAt;
        var checkInTime = TimeOnly.FromTimeSpan(effectiveCheckIn.TimeOfDay);
        if (checkInTime < openTime || checkInTime > closeTime)
        {
            return true;
        }

        if (checkOutAt.HasValue)
        {
            var effectiveCheckOut = shopTimeZone is not null ? TimeZoneInfo.ConvertTime(checkOutAt.Value, shopTimeZone) : checkOutAt.Value;
            var checkOutTime = TimeOnly.FromTimeSpan(effectiveCheckOut.TimeOfDay);
            if (effectiveCheckOut.Date != effectiveCheckIn.Date || checkOutTime < openTime || checkOutTime > closeTime)
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<AttendanceValidationResult> ValidateAndCalculateAsync(
        AppDbContext context,
        int shopId,
        int shopUserId,
        DateOnly workDate,
        DateTimeOffset checkInAt,
        DateTimeOffset? checkOutAt,
        int breakMinutes,
        bool isAdminOverride,
        string? overrideReason,
        string? openTimeStr,
        string? closeTimeStr,
        int? excludeAttendanceId = null,
        TimeZoneInfo? shopTimeZone = null,
        CancellationToken cancellationToken = default)
    {
        if (breakMinutes < 0 || breakMinutes > 720)
        {
            return new AttendanceValidationResult(false, 0m, false, "Break duration must be between 0 and 720 minutes.");
        }

        if (checkOutAt.HasValue)
        {
            if (checkOutAt.Value <= checkInAt)
            {
                return new AttendanceValidationResult(false, 0m, false, "Check-out time must be after check-in time.");
            }

            var totalSpanMinutes = (checkOutAt.Value - checkInAt).TotalMinutes;
            if (totalSpanMinutes > 24 * 60)
            {
                return new AttendanceValidationResult(false, 0m, false, "Single attendance shift cannot exceed 24 hours.");
            }

            if (totalSpanMinutes <= breakMinutes)
            {
                return new AttendanceValidationResult(false, 0m, false, "Break duration must be shorter than total shift duration.");
            }
        }

        var existingShifts = await context.Attendances
            .AsNoTracking()
            .Where(item =>
                item.ShopId == shopId &&
                item.ShopUserId == shopUserId &&
                item.WorkDate == workDate &&
                item.Status != AttendanceStatus.Rejected &&
                (!excludeAttendanceId.HasValue || item.Id != excludeAttendanceId.Value))
            .Select(item => new { item.Id, item.CheckInAt, item.CheckOutAt })
            .ToListAsync(cancellationToken);

        var newEnd = checkOutAt ?? checkInAt.AddHours(24);
        foreach (var shift in existingShifts)
        {
            var existingEnd = shift.CheckOutAt ?? shift.CheckInAt.AddHours(24);
            if (checkInAt < existingEnd && newEnd > shift.CheckInAt)
            {
                return new AttendanceValidationResult(false, 0m, false, "Overlapping attendance entry exists for this worker on the selected date.");
            }
        }

        var outsideHours = IsOutsideOperatingWindow(checkInAt, checkOutAt, openTimeStr, closeTimeStr, shopTimeZone);
        if (outsideHours && !isAdminOverride)
        {
            return new AttendanceValidationResult(
                false,
                0m,
                true,
                $"Logged hours fall outside store operating hours ({openTimeStr ?? "09:00"} - {closeTimeStr ?? "18:00"}). Enable Admin Override with a reason to save.");
        }

        if (outsideHours && string.IsNullOrWhiteSpace(overrideReason))
        {
            return new AttendanceValidationResult(
                false,
                0m,
                true,
                "An override reason is required when logging hours outside store operating hours.");
        }

        var totalHours = CalculateHours(checkInAt, checkOutAt, breakMinutes);
        return new AttendanceValidationResult(true, totalHours, outsideHours);
    }
}

