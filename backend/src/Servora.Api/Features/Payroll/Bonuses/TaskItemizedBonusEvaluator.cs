using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Devices;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Payroll.Bonuses;

public sealed record TaskItemizedRule(
    string TaskKey,
    string TaskLabel,
    string MatcherKeyword,
    string DeviceCategory, // "Any", "Standard", "Gaming"
    decimal RewardPerUnit);

public sealed record TaskItemizedParameters(
    IReadOnlyList<TaskItemizedRule>? Rules = null,
    string? PayoutTrigger = null); // "TicketFinalized" (default) or "TaskCompleted"

public sealed record TaskChecklistEntry(
    string Title,
    string? Description = null);

public sealed record TaskWorkItemEntry(
    string TicketNumber,
    string BonusTaskKey,
    string Title,
    decimal Quantity,
    string? DeviceCategoryOverride,
    Device? Device,
    string? Complaint,
    string? DeviceSnapshot);

public sealed record TaskTicketCandidate(
    string TicketNumber,
    Device? Device,
    string? Complaint,
    string? DeviceSnapshot,
    IReadOnlyList<TaskChecklistEntry> CompletedChecklistItems);

public sealed class TaskItemizedBonusEvaluator(AppDbContext dbContext) : IBonusEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] GamingKeywords =
    [
        "ROG", "TUF", "LEGION", "ALIENWARE", "PREDATOR", "NITRO", "OMEN", "VICTUS", "GAMING", "MSI GAMING", "RAZER", "AORUS",
        "HEAVY", "COMMERCIAL", "INDUSTRIAL", "PREMIUM", "PRO MAX"
    ];

    public string BonusTypeCode => BonusTypeCodes.TaskItemized;

    public async Task<BonusEvaluationResult> EvaluateAsync(
        BonusEvaluationContext context,
        string parametersJson,
        CancellationToken cancellationToken)
    {
        var parameters = ParseParameters(parametersJson);
        var rules = parameters.Rules?
            .Where(r => r.RewardPerUnit > 0 && !string.IsNullOrWhiteSpace(r.TaskLabel))
            .ToList() ?? [];

        if (rules.Count == 0)
        {
            return new BonusEvaluationResult(
                BonusTypeCode,
                "Role-Specific Task Itemized Bonus",
                0,
                0,
                0m,
                0m,
                0m,
                "No active task bonus rules configured",
                []);
        }

        var triggerOnTaskCompleted = string.Equals(parameters.PayoutTrigger?.Trim(), "TaskCompleted", StringComparison.OrdinalIgnoreCase);

        // Stage 1: Explicit ServiceTicketWorkItems linked to a catalog BonusTaskKey
        var explicitWorkItemsQuery = dbContext.ServiceTicketWorkItems
            .AsNoTracking()
            .Include(item => item.ServiceTicket)
                .ThenInclude(t => t.Device)
            .Include(item => item.ServiceTicket)
                .ThenInclude(t => t.Status)
            .Where(item =>
                item.ShopId == context.ShopId &&
                item.BonusTaskKey != null &&
                item.BonusTaskKey != "" &&
                item.IsCompleted &&
                item.Status == WorkItemStatus.Approved &&
                item.ServiceTicket.DeletedAt == null &&
                !item.ServiceTicket.Status.IsCancelled &&
                ((item.CompletedByUserId ?? item.AssignedTechnicianUserId ?? item.ServiceTicket.AssignedTechnicianUserId) == context.UserId));

        if (triggerOnTaskCompleted)
        {
            explicitWorkItemsQuery = explicitWorkItemsQuery.Where(item =>
                (item.CompletedAt ?? item.UpdatedAt ?? item.CreatedAt) >= context.StartDateTimeUtc &&
                (item.CompletedAt ?? item.UpdatedAt ?? item.CreatedAt) < context.EndDateTimeExclusiveUtc);
        }
        else
        {
            explicitWorkItemsQuery = explicitWorkItemsQuery.Where(item =>
                item.ServiceTicket.Status.IsFinal &&
                (item.ServiceTicket.UpdatedAt ?? item.ServiceTicket.CreatedAt) >= context.StartDateTimeUtc &&
                (item.ServiceTicket.UpdatedAt ?? item.ServiceTicket.CreatedAt) < context.EndDateTimeExclusiveUtc);
        }

        var explicitItems = await explicitWorkItemsQuery
            .OrderBy(item => item.CompletedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .ToListAsync(cancellationToken);

        var workItemEntries = explicitItems
            .Select(item => new TaskWorkItemEntry(
                item.ServiceTicket.TicketNumber,
                item.BonusTaskKey!,
                item.Title,
                item.Quantity,
                item.DeviceCategoryOverride,
                item.ServiceTicket.Device,
                item.ServiceTicket.Complaint,
                item.ServiceTicket.DeviceSnapshot))
            .ToList();

        // Stage 2: Legacy checklist keyword fallback for finalized tickets in period
        var tickets = await dbContext.ServiceTickets
            .AsNoTracking()
            .Include(t => t.Device)
            .Where(ticket =>
                ticket.ShopId == context.ShopId &&
                ticket.DeletedAt == null &&
                ticket.AssignedTechnicianUserId == context.UserId &&
                ticket.Status.IsFinal &&
                !ticket.Status.IsCancelled &&
                (ticket.UpdatedAt ?? ticket.CreatedAt) >= context.StartDateTimeUtc &&
                (ticket.UpdatedAt ?? ticket.CreatedAt) < context.EndDateTimeExclusiveUtc)
            .OrderBy(ticket => ticket.UpdatedAt ?? ticket.CreatedAt)
            .ToListAsync(cancellationToken);

        List<TaskTicketCandidate> candidates = [];
        if (tickets.Count > 0)
        {
            var ticketIds = tickets.Select(t => t.Id).ToList();
            var checklistItems = await dbContext.ServiceTicketChecklistItems
                .AsNoTracking()
                .Where(item => item.ShopId == context.ShopId && ticketIds.Contains(item.ServiceTicketId) && item.IsCompleted)
                .ToListAsync(cancellationToken);

            var checklistByTicket = checklistItems
                .GroupBy(c => c.ServiceTicketId)
                .ToDictionary(g => g.Key, g => g.ToList());

            candidates = tickets
                .Select(ticket => new TaskTicketCandidate(
                    ticket.TicketNumber,
                    ticket.Device,
                    ticket.Complaint,
                    ticket.DeviceSnapshot,
                    checklistByTicket.TryGetValue(ticket.Id, out var items)
                        ? items.Select(item => new TaskChecklistEntry(item.Title, item.Description)).ToList()
                        : []))
                .ToList();
        }

        return EvaluateCombined(workItemEntries, candidates, parametersJson);
    }

    public static BonusEvaluationResult EvaluateTickets(
        IReadOnlyList<TaskTicketCandidate> tickets,
        string? parametersJson) =>
        EvaluateCombined([], tickets, parametersJson);

    public static BonusEvaluationResult EvaluateCombined(
        IReadOnlyList<TaskWorkItemEntry> explicitWorkItems,
        IReadOnlyList<TaskTicketCandidate> legacyTickets,
        string? parametersJson)
    {
        var parameters = ParseParameters(parametersJson);
        var rules = parameters.Rules?
            .Where(r => r.RewardPerUnit > 0 && !string.IsNullOrWhiteSpace(r.TaskLabel))
            .ToList() ?? [];

        if (rules.Count == 0 || (explicitWorkItems.Count == 0 && legacyTickets.Count == 0))
        {
            return new BonusEvaluationResult(
                BonusTypeCodes.TaskItemized,
                "Role-Specific Task Itemized Bonus",
                0,
                0,
                0m,
                0m,
                0m,
                rules.Count == 0 ? "No active task bonus rules configured" : "0 completed tickets or tasks in period",
                []);
        }

        var ruleHits = rules
            .Select(rule => (
                Rule: rule,
                NormalizedKey: rule.TaskKey?.Trim().ToUpperInvariant() ?? string.Empty,
                Matchers: BuildKeywordMatchers(rule),
                Units: new List<(string TicketNumber, int Units)>()))
            .ToList();

        // Track (ticketNumber, taskKey) already credited via explicit work items so legacy checklist fallback never double-counts
        var creditedByTicketAndKey = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Stage 1: Explicit work items with BonusTaskKey
        foreach (var item in explicitWorkItems)
        {
            var itemKey = item.BonusTaskKey.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(itemKey)) continue;

            var isGaming = ResolveIsGaming(item.DeviceCategoryOverride, item.Device, item.Complaint, item.DeviceSnapshot);
            var units = Math.Max(1, (int)Math.Floor(item.Quantity));

            // Prefer category-specific rule (Gaming/Standard) over "Any" when both exist for the same TaskKey
            var matchingRule = ruleHits
                .Where(hit => !string.IsNullOrEmpty(hit.NormalizedKey)
                    && string.Equals(hit.NormalizedKey, itemKey, StringComparison.OrdinalIgnoreCase)
                    && MatchesDeviceCategory(hit.Rule.DeviceCategory, isGaming))
                .OrderByDescending(hit => IsSpecificCategory(hit.Rule.DeviceCategory) ? 1 : 0)
                .FirstOrDefault();

            if (matchingRule.Rule is not null)
            {
                matchingRule.Units.Add((item.TicketNumber, units));
                creditedByTicketAndKey.Add($"{item.TicketNumber}::{itemKey}");
            }
        }

        // Stage 2: Legacy checklist keyword fallback
        foreach (var ticket in legacyTickets)
        {
            if (ticket.CompletedChecklistItems.Count == 0)
            {
                continue;
            }

            var isGaming = IsGamingDevice(ticket.Device, ticket.Complaint, ticket.DeviceSnapshot);
            var matchedKeysOnTicket = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hit in ruleHits)
            {
                if (hit.Matchers.Count == 0 || !MatchesDeviceCategory(hit.Rule.DeviceCategory, isGaming))
                {
                    continue;
                }

                var normalizedKey = hit.NormalizedKey;
                if (!string.IsNullOrEmpty(normalizedKey))
                {
                    if (creditedByTicketAndKey.Contains($"{ticket.TicketNumber}::{normalizedKey}"))
                    {
                        continue;
                    }
                    if (matchedKeysOnTicket.Contains(normalizedKey))
                    {
                        continue;
                    }
                }

                var matched = ticket.CompletedChecklistItems.Any(item =>
                    hit.Matchers.Any(matcher =>
                        (!string.IsNullOrEmpty(item.Title) && matcher.IsMatch(item.Title)) ||
                        (!string.IsNullOrEmpty(item.Description) && matcher.IsMatch(item.Description))));

                if (matched)
                {
                    hit.Units.Add((ticket.TicketNumber, 1));
                    if (!string.IsNullOrEmpty(normalizedKey))
                    {
                        matchedKeysOnTicket.Add(normalizedKey);
                    }
                }
            }
        }

        var totalUnits = 0;
        decimal totalAmount = 0m;
        var summarySegments = new List<string>();
        var qualifyingTickets = new HashSet<string>();

        foreach (var (rule, _, _, entries) in ruleHits)
        {
            var count = entries.Sum(e => e.Units);
            if (count <= 0) continue;

            totalUnits += count;
            var subtotal = Math.Round(count * rule.RewardPerUnit, 2, MidpointRounding.AwayFromZero);
            totalAmount += subtotal;

            var devQualifier = rule.DeviceCategory is "Gaming" ? " (Tier 2)" : (rule.DeviceCategory is "Standard" ? " (Std)" : "");
            summarySegments.Add($"{rule.TaskLabel}{devQualifier}: {count} × {rule.RewardPerUnit:N0} = {subtotal:N0}");

            foreach (var (num, _) in entries)
            {
                qualifyingTickets.Add(num);
            }
        }

        var formulaSummary = summarySegments.Count > 0
            ? string.Join(" | ", summarySegments)
            : "No qualifying tasks matched on completed tickets";

        return new BonusEvaluationResult(
            BonusTypeCodes.TaskItemized,
            "Role-Specific Task Itemized Bonus",
            totalUnits,
            totalUnits,
            0m,
            0m,
            totalAmount,
            formulaSummary,
            qualifyingTickets.OrderBy(t => t).ToList());
    }

    public static bool ContainsKeyword(string? text, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        return BuildKeywordRegex(keyword)?.IsMatch(text) == true;
    }

    private static IReadOnlyList<Regex> BuildKeywordMatchers(TaskItemizedRule rule)
    {
        var rawKeywords = (string.IsNullOrWhiteSpace(rule.MatcherKeyword) ? rule.TaskLabel : rule.MatcherKeyword)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var matchers = new List<Regex>(rawKeywords.Length);
        foreach (var keyword in rawKeywords)
        {
            var regex = BuildKeywordRegex(keyword);
            if (regex is not null)
            {
                matchers.Add(regex);
            }
        }

        return matchers;
    }

    private static Regex? BuildKeywordRegex(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        var tokens = keyword.Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Regex.Escape);
        var pattern = $@"(?<![\p{{L}}\p{{N}}]){string.Join(@"\s+", tokens)}(?![\p{{L}}\p{{N}}])";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    public static bool ResolveIsGaming(string? deviceCategoryOverride, Device? device, string? complaint, string? deviceSnapshot)
    {
        if (!string.IsNullOrWhiteSpace(deviceCategoryOverride))
        {
            if (deviceCategoryOverride.Equals("Gaming", StringComparison.OrdinalIgnoreCase)) return true;
            if (deviceCategoryOverride.Equals("Standard", StringComparison.OrdinalIgnoreCase)) return false;
        }

        return IsGamingDevice(device, complaint, deviceSnapshot);
    }

    public static bool IsGamingDevice(Device? device, string? complaint, string? deviceSnapshot)
    {
        var text = $"{device?.Brand} {device?.Model} {device?.Notes} {complaint} {deviceSnapshot}".ToUpperInvariant();
        return GamingKeywords.Any(k => text.Contains(k));
    }

    private static bool IsSpecificCategory(string? category) =>
        !string.IsNullOrWhiteSpace(category) && !category.Equals("Any", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesDeviceCategory(string? category, bool isGaming)
    {
        if (string.IsNullOrWhiteSpace(category) || category.Equals("Any", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (category.Equals("Gaming", StringComparison.OrdinalIgnoreCase))
        {
            return isGaming;
        }

        if (category.Equals("Standard", StringComparison.OrdinalIgnoreCase))
        {
            return !isGaming;
        }

        return true;
    }

    private static TaskItemizedParameters ParseParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new TaskItemizedParameters();
        }

        try
        {
            return JsonSerializer.Deserialize<TaskItemizedParameters>(json, JsonOptions)
                ?? new TaskItemizedParameters();
        }
        catch
        {
            return new TaskItemizedParameters();
        }
    }
}
