using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Payroll.Bonuses;

public sealed record CompletionMilestoneBracket(
    int MinUnits,
    int MaxUnits,
    decimal RewardPerUnit,
    decimal FlatReward = 0m);

public sealed record MaintenanceCompletionParameters(
    int Threshold = 4,
    decimal RewardPerUnit = 15000m,
    decimal FlatReward = 0m,
    bool CountAllUnitsOnceThresholdMet = false,
    bool IsRetroactive = true,
    IReadOnlyList<CompletionMilestoneBracket>? Milestones = null);

public sealed class MaintenanceCompletionBonusEvaluator(AppDbContext dbContext) : IBonusEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public string BonusTypeCode => BonusTypeCodes.MaintenanceCompletion;

    public async Task<BonusEvaluationResult> EvaluateAsync(
        BonusEvaluationContext context,
        string parametersJson,
        CancellationToken cancellationToken)
    {
        var parameters = ParseParameters(parametersJson);
        var threshold = Math.Max(0, parameters.Threshold);
        var rewardPerUnit = Math.Max(0m, parameters.RewardPerUnit);
        var flatReward = Math.Max(0m, parameters.FlatReward);

        var completedTickets = await dbContext.ServiceTickets
            .AsNoTracking()
            .Where(ticket =>
                ticket.ShopId == context.ShopId &&
                ticket.DeletedAt == null &&
                ticket.AssignedTechnicianUserId == context.UserId &&
                ticket.Status.IsFinal &&
                !ticket.Status.IsCancelled &&
                (ticket.UpdatedAt ?? ticket.CreatedAt) >= context.StartDateTimeUtc &&
                (ticket.UpdatedAt ?? ticket.CreatedAt) < context.EndDateTimeExclusiveUtc)
            .OrderBy(ticket => ticket.UpdatedAt ?? ticket.CreatedAt)
            .Select(ticket => ticket.TicketNumber)
            .ToListAsync(cancellationToken);

        var metricCount = completedTickets.Count;

        if (parameters.Milestones is { Count: > 0 } milestones)
        {
            var sorted = milestones
                .Where(m => m.MinUnits >= 0)
                .OrderBy(m => m.MinUnits)
                .ToList();

            if (sorted.Count > 0)
            {
                if (metricCount == 0)
                {
                    return new BonusEvaluationResult(
                        BonusTypeCode,
                        "Service Completion Milestone",
                        0,
                        0,
                        sorted[0].RewardPerUnit,
                        0m,
                        0m,
                        "0 units completed",
                        completedTickets);
                }

                var matched = sorted.FirstOrDefault(m => metricCount >= m.MinUnits && metricCount <= m.MaxUnits);
                var matchedIndex = matched is not null ? sorted.IndexOf(matched) : -1;

                if (matched is null && metricCount > sorted[^1].MaxUnits)
                {
                    matched = sorted[^1];
                    matchedIndex = sorted.Count - 1;
                }

                if (matched is null)
                {
                    return new BonusEvaluationResult(
                        BonusTypeCode,
                        "Service Completion Milestone",
                        metricCount,
                        0,
                        0m,
                        0m,
                        0m,
                        $"{metricCount} units (below milestone threshold of {sorted[0].MinUnits})",
                        completedTickets);
                }

                if (parameters.IsRetroactive)
                {
                    var unitTotal = Math.Round(metricCount * matched.RewardPerUnit, 2, MidpointRounding.AwayFromZero);
                    var totalBonus = Math.Round(unitTotal + matched.FlatReward, 2, MidpointRounding.AwayFromZero);
                    var summary = matched.FlatReward > 0m
                        ? $"{metricCount} units × {matched.RewardPerUnit:N0} + Flat {matched.FlatReward:N0} (Milestone {matchedIndex + 1}: {matched.MinUnits}-{matched.MaxUnits} units retroactive)"
                        : $"{metricCount} units × {matched.RewardPerUnit:N0} (Milestone {matchedIndex + 1}: {matched.MinUnits}+ units retroactive)";

                    return new BonusEvaluationResult(
                        BonusTypeCode,
                        "Service Completion Milestone",
                        metricCount,
                        metricCount,
                        matched.RewardPerUnit,
                        matched.FlatReward,
                        totalBonus,
                        summary,
                        completedTickets);
                }
                else
                {
                    decimal progressiveTotal = 0m;
                    int countedSoFar = 0;
                    var segments = new List<string>();

                    for (int i = 0; i <= matchedIndex; i++)
                    {
                        var m = sorted[i];
                        int bracketCapacity = m.MaxUnits - m.MinUnits + 1;
                        int unitsInBracket = Math.Min(metricCount - countedSoFar, bracketCapacity);
                        if (unitsInBracket <= 0) break;

                        var subtotal = Math.Round(unitsInBracket * m.RewardPerUnit, 2, MidpointRounding.AwayFromZero);
                        progressiveTotal += subtotal;
                        countedSoFar += unitsInBracket;
                        segments.Add($"{unitsInBracket} × {m.RewardPerUnit:N0}");
                    }

                    progressiveTotal += matched.FlatReward;
                    var progressiveSummary = string.Join(" + ", segments);
                    if (matched.FlatReward > 0m) progressiveSummary += $" + Flat {matched.FlatReward:N0}";

                    return new BonusEvaluationResult(
                        BonusTypeCode,
                        "Service Completion Milestone",
                        metricCount,
                        countedSoFar,
                        matched.RewardPerUnit,
                        matched.FlatReward,
                        progressiveTotal,
                        progressiveSummary,
                        completedTickets);
                }
            }
        }

        if (metricCount == 0 || metricCount <= threshold)
        {
            return new BonusEvaluationResult(
                BonusTypeCode,
                "Maintenance Completion Threshold",
                metricCount,
                0,
                rewardPerUnit,
                flatReward,
                0m,
                $"{metricCount} / {threshold} units (threshold not met)",
                completedTickets);
        }

        var qualifiedCount = parameters.CountAllUnitsOnceThresholdMet
            ? metricCount
            : metricCount - threshold;
        var unitAmount = Math.Round(qualifiedCount * rewardPerUnit, 2, MidpointRounding.AwayFromZero);
        var totalAmount = Math.Round(flatReward + unitAmount, 2, MidpointRounding.AwayFromZero);

        var formulaSummary = flatReward > 0m && rewardPerUnit > 0m
            ? $"Flat {flatReward:0} + ({qualifiedCount} units × {rewardPerUnit:0})"
            : flatReward > 0m
                ? $"Milestone {flatReward:0} ({metricCount} > {threshold} units)"
                : $"{qualifiedCount} units × {rewardPerUnit:0} ({metricCount} total > {threshold} threshold)";

        return new BonusEvaluationResult(
            BonusTypeCode,
            "Maintenance Completion Threshold",
            metricCount,
            qualifiedCount,
            rewardPerUnit,
            flatReward,
            totalAmount,
            formulaSummary,
            completedTickets);
    }

    private static MaintenanceCompletionParameters ParseParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new MaintenanceCompletionParameters();
        }

        try
        {
            return JsonSerializer.Deserialize<MaintenanceCompletionParameters>(json, JsonOptions)
                ?? new MaintenanceCompletionParameters();
        }
        catch
        {
            return new MaintenanceCompletionParameters();
        }
    }
}

