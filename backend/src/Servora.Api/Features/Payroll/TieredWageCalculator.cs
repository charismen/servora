using System.Text.Json;
using System.Text.Json.Serialization;
using Servora.Api.Features.Attendance;

namespace Servora.Api.Features.Payroll;

public sealed record TierBreakdownItemDto(
    string TierLabel,
    int ShiftCount,
    decimal TotalHours,
    decimal RateApplied,
    decimal SubtotalAmount);

public sealed record BaseWageCalculationResult(
    BasePayMode BasePayMode,
    int TotalDaysWorked,
    decimal TotalHoursWorked,
    decimal BaseCalculatedWage,
    IReadOnlyList<TierBreakdownItemDto> Breakdown);

public static class TieredWageCalculator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<WageTierBracket> ParseBrackets(string? tieredRatesJson)
    {
        if (string.IsNullOrWhiteSpace(tieredRatesJson))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<WageTierBracket>>(tieredRatesJson, JsonOptions);
            return parsed?
                .Where(bracket => bracket.MaxHours >= bracket.MinHours && bracket.MinHours >= 0)
                .OrderBy(bracket => bracket.MinHours)
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static FreelancePayPolicy ParsePayPolicy(string? payPolicyJson)
    {
        if (string.IsNullOrWhiteSpace(payPolicyJson))
        {
            return new FreelancePayPolicy();
        }

        try
        {
            return JsonSerializer.Deserialize<FreelancePayPolicy>(payPolicyJson, JsonOptions)
                ?? new FreelancePayPolicy();
        }
        catch
        {
            return new FreelancePayPolicy();
        }
    }

    public static BaseWageCalculationResult Calculate(
        BasePayMode basePayMode,
        decimal baseHourlyRate,
        string? tieredRatesJson,
        IReadOnlyCollection<AttendanceRecord> approvedAttendances,
        FreelancePayPolicy? payPolicy = null,
        decimal completedServiceRevenue = 0m)
    {
        var policy = payPolicy ?? new FreelancePayPolicy();
        var completedShifts = approvedAttendances
            .Where(shift => shift.Status == AttendanceStatus.Approved && shift.CheckOutAt.HasValue && shift.TotalHours > 0)
            .OrderBy(shift => shift.WorkDate)
            .ThenBy(shift => shift.CheckInAt)
            .ToList();

        var totalDays = completedShifts.Select(shift => shift.WorkDate).Distinct().Count();
        var totalHours = Math.Round(completedShifts.Sum(shift => shift.TotalHours), 2, MidpointRounding.AwayFromZero);

        if (basePayMode == BasePayMode.CommissionOnly)
        {
            return new BaseWageCalculationResult(
                basePayMode,
                totalDays,
                totalHours,
                0m,
                [new TierBreakdownItemDto("Commission Only (Bonuses & Tasks)", completedShifts.Count, totalHours, 0m, 0m)]);
        }

        if (basePayMode == BasePayMode.RevenueShare)
        {
            var sharePercent = Math.Max(0m, policy.RevenueSharePercent);
            var revenue = Math.Max(0m, completedServiceRevenue);
            var amount = Math.Round(revenue * sharePercent / 100m, 2, MidpointRounding.AwayFromZero);
            var label = $"Revenue Share ({sharePercent:0.##}% of {revenue:N0})";
            return new BaseWageCalculationResult(
                basePayMode,
                totalDays,
                totalHours,
                amount,
                [new TierBreakdownItemDto(label, completedShifts.Count, totalHours, sharePercent, amount)]);
        }

        if (basePayMode == BasePayMode.FixedMonthly)
        {
            var salary = Math.Max(0m, policy.FixedMonthlySalary);
            var standardDays = Math.Max(1, policy.StandardWorkDaysPerMonth);
            decimal amount;
            string label;

            if (policy.ProrateMonthlyByAttendance)
            {
                var ratio = Math.Min(1m, (decimal)totalDays / standardDays);
                amount = Math.Round(salary * ratio, 2, MidpointRounding.AwayFromZero);
                label = $"Fixed Monthly (Prorated {totalDays}/{standardDays}d)";
            }
            else
            {
                amount = Math.Round(salary, 2, MidpointRounding.AwayFromZero);
                label = "Fixed Monthly Retainer";
            }

            return new BaseWageCalculationResult(
                basePayMode,
                totalDays,
                totalHours,
                amount,
                [new TierBreakdownItemDto(label, totalDays, totalHours, salary, amount)]);
        }

        if (completedShifts.Count == 0)
        {
            return new BaseWageCalculationResult(basePayMode, 0, 0m, 0m, []);
        }

        if (basePayMode == BasePayMode.FixedDaily)
        {
            var dailyRate = Math.Max(0m, policy.FixedDailyRate);
            var weekendMult = policy.WeekendMultiplier > 0m ? policy.WeekendMultiplier : 1.0m;
            var distinctDays = completedShifts
                .GroupBy(s => s.WorkDate)
                .OrderBy(g => g.Key)
                .ToList();

            var weekdayCount = 0;
            var weekdayHours = 0m;
            var weekendCount = 0;
            var weekendHours = 0m;

            foreach (var dayGroup in distinctDays)
            {
                var dayHours = dayGroup.Sum(s => s.TotalHours);
                if (IsWeekend(dayGroup.Key) && weekendMult != 1.0m)
                {
                    weekendCount++;
                    weekendHours += dayHours;
                }
                else
                {
                    weekdayCount++;
                    weekdayHours += dayHours;
                }
            }

            var lines = new List<TierBreakdownItemDto>();
            if (weekdayCount > 0)
            {
                var sub = Math.Round(weekdayCount * dailyRate, 2, MidpointRounding.AwayFromZero);
                lines.Add(new TierBreakdownItemDto("Fixed Daily Rate", weekdayCount, Math.Round(weekdayHours, 2, MidpointRounding.AwayFromZero), dailyRate, sub));
            }
            if (weekendCount > 0)
            {
                var wkRate = Math.Round(dailyRate * weekendMult, 2, MidpointRounding.AwayFromZero);
                var sub = Math.Round(weekendCount * wkRate, 2, MidpointRounding.AwayFromZero);
                lines.Add(new TierBreakdownItemDto($"Fixed Daily (Weekend ×{weekendMult:0.##})", weekendCount, Math.Round(weekendHours, 2, MidpointRounding.AwayFromZero), wkRate, sub));
            }

            var totalFixed = Math.Round(lines.Sum(l => l.SubtotalAmount), 2, MidpointRounding.AwayFromZero);
            return new BaseWageCalculationResult(basePayMode, totalDays, totalHours, totalFixed, lines);
        }

        if (basePayMode == BasePayMode.Hourly)
        {
            var hasOvertime = policy.OvertimeThresholdHoursPerDay is > 0m && policy.OvertimeMultiplier > 1.0m;
            var hasWeekend = policy.WeekendMultiplier > 0m && policy.WeekendMultiplier != 1.0m;

            if (!hasOvertime && !hasWeekend)
            {
                var amount = Math.Round(totalHours * baseHourlyRate, 2, MidpointRounding.AwayFromZero);
                var line = new TierBreakdownItemDto("Hourly Base Rate", completedShifts.Count, totalHours, baseHourlyRate, amount);
                return new BaseWageCalculationResult(basePayMode, totalDays, totalHours, amount, [line]);
            }

            var regularHours = 0m;
            var regularShifts = 0;
            var overtimeHours = 0m;
            var overtimeShifts = 0;
            var weekendHours = 0m;
            var weekendShifts = 0;

            foreach (var shift in completedShifts)
            {
                var hours = shift.TotalHours;
                if (hasWeekend && IsWeekend(shift.WorkDate))
                {
                    weekendHours += hours;
                    weekendShifts++;
                }
                else if (hasOvertime && hours > policy.OvertimeThresholdHoursPerDay!.Value)
                {
                    var threshold = policy.OvertimeThresholdHoursPerDay.Value;
                    regularHours += threshold;
                    regularShifts++;
                    overtimeHours += hours - threshold;
                    overtimeShifts++;
                }
                else
                {
                    regularHours += hours;
                    regularShifts++;
                }
            }

            var lines = new List<TierBreakdownItemDto>();
            if (regularHours > 0m)
            {
                var roundedHrs = Math.Round(regularHours, 2, MidpointRounding.AwayFromZero);
                var sub = Math.Round(regularHours * baseHourlyRate, 2, MidpointRounding.AwayFromZero);
                lines.Add(new TierBreakdownItemDto("Hourly Base Rate", regularShifts, roundedHrs, baseHourlyRate, sub));
            }
            if (overtimeHours > 0m)
            {
                var otRate = Math.Round(baseHourlyRate * policy.OvertimeMultiplier, 2, MidpointRounding.AwayFromZero);
                var roundedHrs = Math.Round(overtimeHours, 2, MidpointRounding.AwayFromZero);
                var sub = Math.Round(overtimeHours * otRate, 2, MidpointRounding.AwayFromZero);
                lines.Add(new TierBreakdownItemDto($"Overtime (>{policy.OvertimeThresholdHoursPerDay:0.##}h · ×{policy.OvertimeMultiplier:0.##})", overtimeShifts, roundedHrs, otRate, sub));
            }
            if (weekendHours > 0m)
            {
                var wkRate = Math.Round(baseHourlyRate * policy.WeekendMultiplier, 2, MidpointRounding.AwayFromZero);
                var roundedHrs = Math.Round(weekendHours, 2, MidpointRounding.AwayFromZero);
                var sub = Math.Round(weekendHours * wkRate, 2, MidpointRounding.AwayFromZero);
                lines.Add(new TierBreakdownItemDto($"Weekend Hours (×{policy.WeekendMultiplier:0.##})", weekendShifts, roundedHrs, wkRate, sub));
            }

            var totalHourlyAmount = Math.Round(lines.Sum(l => l.SubtotalAmount), 2, MidpointRounding.AwayFromZero);
            return new BaseWageCalculationResult(basePayMode, totalDays, totalHours, totalHourlyAmount, lines);
        }

        var brackets = ParseBrackets(tieredRatesJson);
        if (brackets.Count == 0)
        {
            var fallbackAmount = policy.FallbackStrategy == FallbackPayStrategy.Zero
                ? 0m
                : Math.Round(totalHours * baseHourlyRate, 2, MidpointRounding.AwayFromZero);
            var fallbackLabel = policy.FallbackStrategy == FallbackPayStrategy.Zero
                ? "No Tier Brackets (Zero Fallback)"
                : "Prorated / Hourly Fallback";
            return new BaseWageCalculationResult(
                basePayMode,
                totalDays,
                totalHours,
                fallbackAmount,
                [new TierBreakdownItemDto(fallbackLabel, completedShifts.Count, totalHours, policy.FallbackStrategy == FallbackPayStrategy.Zero ? 0m : baseHourlyRate, fallbackAmount)]);
        }

        if (basePayMode == BasePayMode.TieredMonthly)
        {
            var matchedMonthly = MatchBracket(totalHours, brackets);
            if (matchedMonthly is not null)
            {
                var amount = ComputeBracketPay(totalHours, matchedMonthly.Value.Bracket);
                var label = $"Monthly Tier {matchedMonthly.Value.Index + 1} ({matchedMonthly.Value.Bracket.MinHours:0.##}-{matchedMonthly.Value.Bracket.MaxHours:0.##}h)";
                return new BaseWageCalculationResult(
                    basePayMode,
                    totalDays,
                    totalHours,
                    amount,
                    [new TierBreakdownItemDto(label, completedShifts.Count, totalHours, matchedMonthly.Value.Bracket.FlatRate, amount)]);
            }

            var proratedMonthly = ComputeFallbackPay(totalHours, brackets, baseHourlyRate, policy.FallbackStrategy);
            return new BaseWageCalculationResult(
                basePayMode,
                totalDays,
                totalHours,
                proratedMonthly.Amount,
                [new TierBreakdownItemDto(proratedMonthly.Label, completedShifts.Count, totalHours, proratedMonthly.RateUsed, proratedMonthly.Amount)]);
        }

        if (basePayMode == BasePayMode.TieredDailyAverage)
        {
            var averageDailyHours = totalDays > 0 ? Math.Round(totalHours / totalDays, 2, MidpointRounding.AwayFromZero) : 0m;
            var matchedAverage = MatchBracket(averageDailyHours, brackets);

            if (matchedAverage is not null)
            {
                var bracket = matchedAverage.Value.Bracket;
                var dailyRate = ComputeBracketPay(averageDailyHours, bracket);
                var amount = Math.Round(dailyRate * totalDays, 2, MidpointRounding.AwayFromZero);
                var label = $"Tier {matchedAverage.Value.Index + 1} ({bracket.MinHours:0.##}-{bracket.MaxHours:0.##}h/day · avg {averageDailyHours:0.##}h/day)";
                var line = new TierBreakdownItemDto(
                    label,
                    totalDays,
                    totalHours,
                    dailyRate,
                    amount);
                return new BaseWageCalculationResult(basePayMode, totalDays, totalHours, amount, [line]);
            }

            var fallback = ComputeFallbackPay(averageDailyHours, brackets, baseHourlyRate, policy.FallbackStrategy);
            var fallbackDailyRate = fallback.Amount;
            var fallbackTotalAmount = Math.Round(fallbackDailyRate * totalDays, 2, MidpointRounding.AwayFromZero);
            var fallbackLabel = $"{fallback.Label} (avg {averageDailyHours:0.##}h/day)";
            var fallbackLine = new TierBreakdownItemDto(
                fallbackLabel,
                totalDays,
                totalHours,
                fallback.RateUsed > 0m ? fallback.RateUsed : fallbackDailyRate,
                fallbackTotalAmount);
            return new BaseWageCalculationResult(basePayMode, totalDays, totalHours, fallbackTotalAmount, [fallbackLine]);
        }

        // TieredDaily mode: evaluate each approved shift deterministically (with optional weekend multiplier)
        var weekendMultiplier = policy.WeekendMultiplier > 0m ? policy.WeekendMultiplier : 1.0m;
        var buckets = new Dictionary<string, (int Count, decimal Hours, decimal Rate, decimal Amount)>();

        foreach (var shift in completedShifts)
        {
            var hours = shift.TotalHours;
            var matched = MatchBracket(hours, brackets);
            string label;
            decimal rate;
            decimal pay;

            if (matched is not null)
            {
                var bracket = matched.Value.Bracket;
                label = $"Tier {matched.Value.Index + 1} ({bracket.MinHours:0.##}-{bracket.MaxHours:0.##}h/day)";
                rate = bracket.FlatRate > 0 ? bracket.FlatRate : (bracket.HourlyRate ?? baseHourlyRate);
                pay = ComputeBracketPay(hours, bracket);
            }
            else
            {
                var fallback = ComputeFallbackPay(hours, brackets, baseHourlyRate, policy.FallbackStrategy);
                label = fallback.Label;
                rate = fallback.RateUsed;
                pay = fallback.Amount;
            }

            if (weekendMultiplier != 1.0m && IsWeekend(shift.WorkDate) && pay > 0m)
            {
                label = $"{label} [Weekend ×{weekendMultiplier:0.##}]";
                pay = Math.Round(pay * weekendMultiplier, 2, MidpointRounding.AwayFromZero);
                rate = Math.Round(rate * weekendMultiplier, 2, MidpointRounding.AwayFromZero);
            }

            if (buckets.TryGetValue(label, out var existing))
            {
                buckets[label] = (
                    existing.Count + 1,
                    Math.Round(existing.Hours + hours, 2, MidpointRounding.AwayFromZero),
                    rate,
                    Math.Round(existing.Amount + pay, 2, MidpointRounding.AwayFromZero));
            }
            else
            {
                buckets[label] = (1, hours, rate, pay);
            }
        }

        var breakdown = buckets
            .Select(kvp => new TierBreakdownItemDto(kvp.Key, kvp.Value.Count, kvp.Value.Hours, kvp.Value.Rate, kvp.Value.Amount))
            .ToList();
        var totalBaseWage = Math.Round(breakdown.Sum(item => item.SubtotalAmount), 2, MidpointRounding.AwayFromZero);

        return new BaseWageCalculationResult(basePayMode, totalDays, totalHours, totalBaseWage, breakdown);
    }

    private static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static (int Index, WageTierBracket Bracket)? MatchBracket(decimal hours, IReadOnlyList<WageTierBracket> brackets)
    {
        for (var i = 0; i < brackets.Count; i++)
        {
            var bracket = brackets[i];
            if (hours >= bracket.MinHours && hours <= bracket.MaxHours)
            {
                return (i, bracket);
            }
        }

        return null;
    }

    private static decimal ComputeBracketPay(decimal hours, WageTierBracket bracket)
    {
        var total = bracket.FlatRate;
        if (bracket.HourlyRate is > 0m)
        {
            if (bracket.FlatRate > 0m)
            {
                var overtimeHours = Math.Max(0m, hours - bracket.MinHours);
                total += overtimeHours * bracket.HourlyRate.Value;
            }
            else
            {
                total += hours * bracket.HourlyRate.Value;
            }
        }

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static (string Label, decimal RateUsed, decimal Amount) ComputeFallbackPay(
        decimal hours,
        IReadOnlyList<WageTierBracket> brackets,
        decimal baseHourlyRate,
        FallbackPayStrategy fallbackStrategy)
    {
        if (fallbackStrategy == FallbackPayStrategy.Zero)
        {
            return ("Outside Tier Range (Zero Fallback)", 0m, 0m);
        }

        var lowerBracket = brackets
            .Select((bracket, index) => (Index: index, Bracket: bracket))
            .Where(item => hours > item.Bracket.MaxHours)
            .OrderByDescending(item => item.Bracket.MaxHours)
            .FirstOrDefault();

        if (fallbackStrategy == FallbackPayStrategy.LowerTierFloor)
        {
            if (lowerBracket.Bracket is not null)
            {
                var floorPay = ComputeBracketPay(lowerBracket.Bracket.MaxHours, lowerBracket.Bracket);
                return ($"Tier {lowerBracket.Index + 1} Floor ({lowerBracket.Bracket.MinHours:0.##}-{lowerBracket.Bracket.MaxHours:0.##}h)", lowerBracket.Bracket.FlatRate, floorPay);
            }

            return ("Below Minimum Tier (< Min Hours)", 0m, 0m);
        }

        if (baseHourlyRate > 0m)
        {
            var prorated = Math.Round(hours * baseHourlyRate, 2, MidpointRounding.AwayFromZero);
            if (lowerBracket.Bracket is not null)
            {
                var lowerFloor = ComputeBracketPay(lowerBracket.Bracket.MaxHours, lowerBracket.Bracket);
                var extraHours = Math.Max(0m, hours - lowerBracket.Bracket.MaxHours);
                var combined = Math.Round(lowerFloor + (extraHours * baseHourlyRate), 2, MidpointRounding.AwayFromZero);
                return ($"Tier {lowerBracket.Index + 1} + Prorated ({baseHourlyRate:0}/h)", baseHourlyRate, Math.Max(lowerFloor, Math.Min(prorated > lowerFloor ? prorated : combined, combined)));
            }

            return ($"Prorated ({baseHourlyRate:0}/h)", baseHourlyRate, prorated);
        }

        if (lowerBracket.Bracket is not null)
        {
            var floorPay = ComputeBracketPay(lowerBracket.Bracket.MaxHours, lowerBracket.Bracket);
            return ($"Tier {lowerBracket.Index + 1} Floor ({lowerBracket.Bracket.MinHours:0.##}-{lowerBracket.Bracket.MaxHours:0.##}h)", lowerBracket.Bracket.FlatRate, floorPay);
        }

        return ("Below Minimum Tier (< Min Hours)", 0m, 0m);
    }
}
