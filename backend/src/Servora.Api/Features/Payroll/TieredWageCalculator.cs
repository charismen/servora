using System.Text.Json;
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
        PropertyNameCaseInsensitive = true
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

    public static BaseWageCalculationResult Calculate(
        BasePayMode basePayMode,
        decimal baseHourlyRate,
        string? tieredRatesJson,
        IReadOnlyCollection<AttendanceRecord> approvedAttendances)
    {
        var completedShifts = approvedAttendances
            .Where(shift => shift.Status == AttendanceStatus.Approved && shift.CheckOutAt.HasValue && shift.TotalHours > 0)
            .OrderBy(shift => shift.WorkDate)
            .ThenBy(shift => shift.CheckInAt)
            .ToList();

        var totalDays = completedShifts.Select(shift => shift.WorkDate).Distinct().Count();
        var totalHours = Math.Round(completedShifts.Sum(shift => shift.TotalHours), 2, MidpointRounding.AwayFromZero);

        if (completedShifts.Count == 0)
        {
            return new BaseWageCalculationResult(basePayMode, 0, 0m, 0m, []);
        }

        if (basePayMode == BasePayMode.Hourly)
        {
            var amount = Math.Round(totalHours * baseHourlyRate, 2, MidpointRounding.AwayFromZero);
            var line = new TierBreakdownItemDto(
                "Hourly Base Rate",
                completedShifts.Count,
                totalHours,
                baseHourlyRate,
                amount);
            return new BaseWageCalculationResult(basePayMode, totalDays, totalHours, amount, [line]);
        }

        var brackets = ParseBrackets(tieredRatesJson);
        if (brackets.Count == 0)
        {
            var fallbackAmount = Math.Round(totalHours * baseHourlyRate, 2, MidpointRounding.AwayFromZero);
            return new BaseWageCalculationResult(
                basePayMode,
                totalDays,
                totalHours,
                fallbackAmount,
                [new TierBreakdownItemDto("Prorated / Hourly Fallback", completedShifts.Count, totalHours, baseHourlyRate, fallbackAmount)]);
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

            var proratedMonthly = ComputeFallbackPay(totalHours, brackets, baseHourlyRate);
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

            var fallback = ComputeFallbackPay(averageDailyHours, brackets, baseHourlyRate);
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

        // TieredDaily mode: evaluate each approved shift deterministically
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
                var fallback = ComputeFallbackPay(hours, brackets, baseHourlyRate);
                label = fallback.Label;
                rate = fallback.RateUsed;
                pay = fallback.Amount;
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
        decimal baseHourlyRate)
    {
        var lowerBracket = brackets
            .Select((bracket, index) => (Index: index, Bracket: bracket))
            .Where(item => hours > item.Bracket.MaxHours)
            .OrderByDescending(item => item.Bracket.MaxHours)
            .FirstOrDefault();

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

