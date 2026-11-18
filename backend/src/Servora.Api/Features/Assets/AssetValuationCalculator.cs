namespace Servora.Api.Features.Assets;

public sealed record AssetValuationBreakdown(
    decimal PurchaseCost,
    decimal SalvageValue,
    decimal DepreciableBase,
    int UsefulLifeMonths,
    int AgeMonths,
    int AgeYearsPart,
    int AgeMonthsPart,
    int ElapsedDepreciableMonths,
    int RemainingLifeMonths,
    decimal LifeConsumedPercent,
    decimal MonthlyDepreciation,
    decimal AccumulatedDepreciation,
    decimal CurrentBookValue,
    decimal EffectiveSellingValue,
    decimal EstimatedGainLoss,
    decimal TotalMaintenanceCost,
    decimal NetTco,
    bool IsEolApproaching,
    bool IsFullyDepreciated,
    bool IsWarrantyActive);

public sealed record AssetDepreciationScheduleRow(
    int YearNumber,
    int StartMonth,
    int EndMonth,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    decimal OpeningBookValue,
    decimal DepreciationExpense,
    decimal AccumulatedDepreciation,
    decimal ClosingBookValue,
    bool IsCurrentPeriod);

public static class AssetValuationCalculator
{
    public static AssetValuationBreakdown Calculate(
        Asset asset,
        decimal totalMaintenanceCost = 0m,
        DateTimeOffset? asOf = null)
    {
        var referenceDate = asset.DisposedAt ?? asOf ?? DateTimeOffset.UtcNow;
        var startDate = asset.InServiceDate != default ? asset.InServiceDate : asset.PurchaseDate;

        var ageMonths = CalculateWholeMonths(startDate, referenceDate);
        var ageYearsPart = ageMonths / 12;
        var ageMonthsPart = ageMonths % 12;

        var purchaseCost = Math.Max(0m, asset.PurchaseCost);
        var salvageValue = Math.Clamp(asset.SalvageValue, 0m, purchaseCost);
        var depreciableBase = Math.Max(0m, purchaseCost - salvageValue);
        var usefulLifeMonths = Math.Max(0, asset.UsefulLifeMonths);

        decimal monthlyDepreciation = 0m;
        decimal accumulatedDepreciation = 0m;
        int elapsedDepreciableMonths = 0;
        int remainingLifeMonths = 0;
        decimal lifeConsumedPercent = 0m;

        if (asset.DepreciationMethod == DepreciationMethod.StraightLine && usefulLifeMonths > 0 && depreciableBase > 0m)
        {
            monthlyDepreciation = Math.Round(depreciableBase / usefulLifeMonths, 2, MidpointRounding.AwayFromZero);
            elapsedDepreciableMonths = Math.Clamp(ageMonths, 0, usefulLifeMonths);
            remainingLifeMonths = Math.Max(0, usefulLifeMonths - elapsedDepreciableMonths);
            lifeConsumedPercent = Math.Round((decimal)elapsedDepreciableMonths / usefulLifeMonths * 100m, 1, MidpointRounding.AwayFromZero);

            if (elapsedDepreciableMonths >= usefulLifeMonths)
            {
                accumulatedDepreciation = depreciableBase;
            }
            else
            {
                accumulatedDepreciation = Math.Min(depreciableBase, Math.Round(elapsedDepreciableMonths * monthlyDepreciation, 2, MidpointRounding.AwayFromZero));
            }
        }
        else if (usefulLifeMonths > 0)
        {
            elapsedDepreciableMonths = Math.Clamp(ageMonths, 0, usefulLifeMonths);
            remainingLifeMonths = Math.Max(0, usefulLifeMonths - elapsedDepreciableMonths);
            lifeConsumedPercent = Math.Round((decimal)elapsedDepreciableMonths / usefulLifeMonths * 100m, 1, MidpointRounding.AwayFromZero);
        }

        var currentBookValue = Math.Max(salvageValue, purchaseCost - accumulatedDepreciation);

        var effectiveSellingValue = asset.Status == AssetStatus.Sold && asset.ActualSoldPrice.HasValue
            ? asset.ActualSoldPrice.Value
            : asset.Status == AssetStatus.WrittenOff
                ? 0m
                : asset.EstimatedSellingValue ?? currentBookValue;

        var estimatedGainLoss = effectiveSellingValue - currentBookValue;
        var netTco = purchaseCost + totalMaintenanceCost - effectiveSellingValue;

        var isFullyDepreciated = asset.DepreciationMethod == DepreciationMethod.StraightLine
            && usefulLifeMonths > 0
            && elapsedDepreciableMonths >= usefulLifeMonths;

        var isEolApproaching = !isFullyDepreciated
            && usefulLifeMonths > 0
            && lifeConsumedPercent >= 85m;

        var now = asOf ?? DateTimeOffset.UtcNow;
        var isWarrantyActive = asset.WarrantyExpiresAt.HasValue && asset.WarrantyExpiresAt.Value >= now;

        return new AssetValuationBreakdown(
            PurchaseCost: purchaseCost,
            SalvageValue: salvageValue,
            DepreciableBase: depreciableBase,
            UsefulLifeMonths: usefulLifeMonths,
            AgeMonths: ageMonths,
            AgeYearsPart: ageYearsPart,
            AgeMonthsPart: ageMonthsPart,
            ElapsedDepreciableMonths: elapsedDepreciableMonths,
            RemainingLifeMonths: remainingLifeMonths,
            LifeConsumedPercent: lifeConsumedPercent,
            MonthlyDepreciation: monthlyDepreciation,
            AccumulatedDepreciation: accumulatedDepreciation,
            CurrentBookValue: currentBookValue,
            EffectiveSellingValue: effectiveSellingValue,
            EstimatedGainLoss: estimatedGainLoss,
            TotalMaintenanceCost: totalMaintenanceCost,
            NetTco: netTco,
            IsEolApproaching: isEolApproaching,
            IsFullyDepreciated: isFullyDepreciated,
            IsWarrantyActive: isWarrantyActive);
    }

    public static IReadOnlyList<AssetDepreciationScheduleRow> BuildAnnualSchedule(
        Asset asset,
        DateTimeOffset? asOf = null)
    {
        var purchaseCost = Math.Max(0m, asset.PurchaseCost);
        var salvageValue = Math.Clamp(asset.SalvageValue, 0m, purchaseCost);
        var depreciableBase = Math.Max(0m, purchaseCost - salvageValue);
        var usefulLifeMonths = Math.Max(0, asset.UsefulLifeMonths);
        var startDate = asset.InServiceDate != default ? asset.InServiceDate : asset.PurchaseDate;
        var now = asset.DisposedAt ?? asOf ?? DateTimeOffset.UtcNow;

        if (asset.DepreciationMethod == DepreciationMethod.None || usefulLifeMonths <= 0 || depreciableBase <= 0m)
        {
            return
            [
                new AssetDepreciationScheduleRow(
                    YearNumber: 1,
                    StartMonth: 1,
                    EndMonth: Math.Max(1, usefulLifeMonths),
                    PeriodStart: startDate,
                    PeriodEnd: startDate.AddMonths(Math.Max(1, usefulLifeMonths)),
                    OpeningBookValue: purchaseCost,
                    DepreciationExpense: 0m,
                    AccumulatedDepreciation: 0m,
                    ClosingBookValue: purchaseCost,
                    IsCurrentPeriod: true)
            ];
        }

        var monthlyDepreciation = Math.Round(depreciableBase / usefulLifeMonths, 2, MidpointRounding.AwayFromZero);
        var totalYears = (int)Math.Ceiling(usefulLifeMonths / 12.0);
        var rows = new List<AssetDepreciationScheduleRow>(totalYears);

        var openingBookValue = purchaseCost;
        var accumulated = 0m;

        for (var year = 1; year <= totalYears; year++)
        {
            var startMonth = (year - 1) * 12 + 1;
            var endMonth = Math.Min(year * 12, usefulLifeMonths);
            var monthsInPeriod = endMonth - startMonth + 1;

            var periodStart = startDate.AddMonths(startMonth - 1);
            var periodEnd = startDate.AddMonths(endMonth);

            decimal expense;
            if (year == totalYears)
            {
                expense = Math.Max(0m, depreciableBase - accumulated);
            }
            else
            {
                expense = Math.Min(depreciableBase - accumulated, Math.Round(monthsInPeriod * monthlyDepreciation, 2, MidpointRounding.AwayFromZero));
            }

            accumulated += expense;
            var closingBookValue = Math.Max(salvageValue, purchaseCost - accumulated);
            var isCurrentPeriod = now >= periodStart && now < periodEnd;

            rows.Add(new AssetDepreciationScheduleRow(
                YearNumber: year,
                StartMonth: startMonth,
                EndMonth: endMonth,
                PeriodStart: periodStart,
                PeriodEnd: periodEnd,
                OpeningBookValue: openingBookValue,
                DepreciationExpense: expense,
                AccumulatedDepreciation: accumulated,
                ClosingBookValue: closingBookValue,
                IsCurrentPeriod: isCurrentPeriod));

            openingBookValue = closingBookValue;
        }

        return rows;
    }

    public static int CalculateWholeMonths(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from) return 0;
        var months = (to.Year - from.Year) * 12 + (to.Month - from.Month);
        if (to.Day < from.Day)
        {
            months--;
        }
        return Math.Max(0, months);
    }
}
