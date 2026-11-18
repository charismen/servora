using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Devices;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Payroll.Bonuses;

public sealed record TaskItemizedRule(
    string TaskKey,
    string TaskLabel,
    string MatcherKeyword,
    string DeviceCategory, // "Any", "Standard", "Gaming"
    decimal RewardPerUnit);

public sealed record TaskItemizedParameters(
    IReadOnlyList<TaskItemizedRule>? Rules = null);

public sealed record TaskChecklistEntry(
    string Title,
    string? Description = null);

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
        "ROG", "TUF", "LEGION", "ALIENWARE", "PREDATOR", "NITRO", "OMEN", "VICTUS", "GAMING", "MSI GAMING", "RAZER", "AORUS"
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

        if (tickets.Count == 0 || rules.Count == 0)
        {
            return new BonusEvaluationResult(
                BonusTypeCode,
                "Role-Specific Task Itemized Bonus",
                0,
                0,
                0m,
                0m,
                0m,
                rules.Count == 0 ? "No active task bonus rules configured" : "0 completed tickets in period",
                []);
        }

        var ticketIds = tickets.Select(t => t.Id).ToList();
        var checklistItems = await dbContext.ServiceTicketChecklistItems
            .AsNoTracking()
            .Where(item => item.ShopId == context.ShopId && ticketIds.Contains(item.ServiceTicketId) && item.IsCompleted)
            .ToListAsync(cancellationToken);

        var checklistByTicket = checklistItems
            .GroupBy(c => c.ServiceTicketId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var candidates = tickets
            .Select(ticket => new TaskTicketCandidate(
                ticket.TicketNumber,
                ticket.Device,
                ticket.Complaint,
                ticket.DeviceSnapshot,
                checklistByTicket.TryGetValue(ticket.Id, out var items)
                    ? items.Select(item => new TaskChecklistEntry(item.Title, item.Description)).ToList()
                    : []))
            .ToList();

        return EvaluateTickets(candidates, parametersJson);
    }

    public static BonusEvaluationResult EvaluateTickets(
        IReadOnlyList<TaskTicketCandidate> tickets,
        string? parametersJson)
    {
        var parameters = ParseParameters(parametersJson);
        var rules = parameters.Rules?
            .Where(r => r.RewardPerUnit > 0 && !string.IsNullOrWhiteSpace(r.TaskLabel))
            .ToList() ?? [];

        if (tickets.Count == 0 || rules.Count == 0)
        {
            return new BonusEvaluationResult(
                BonusTypeCodes.TaskItemized,
                "Role-Specific Task Itemized Bonus",
                0,
                0,
                0m,
                0m,
                0m,
                rules.Count == 0 ? "No active task bonus rules configured" : "0 completed tickets in period",
                []);
        }

        var ruleHits = rules
            .Select(rule => (Rule: rule, Matchers: BuildKeywordMatchers(rule), TicketNumbers: new List<string>()))
            .ToList();

        foreach (var ticket in tickets)
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

                var normalizedKey = hit.Rule.TaskKey?.Trim();
                if (!string.IsNullOrEmpty(normalizedKey) && matchedKeysOnTicket.Contains(normalizedKey))
                {
                    continue;
                }

                // Cap each task rule to at most 1 credit per ticket, even if multiple
                // completed checklist items on that ticket contain matching keywords.
                var matched = ticket.CompletedChecklistItems.Any(item =>
                    hit.Matchers.Any(matcher =>
                        (!string.IsNullOrEmpty(item.Title) && matcher.IsMatch(item.Title)) ||
                        (!string.IsNullOrEmpty(item.Description) && matcher.IsMatch(item.Description))));

                if (matched)
                {
                    hit.TicketNumbers.Add(ticket.TicketNumber);
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

        foreach (var (rule, _, ticketNums) in ruleHits)
        {
            var count = ticketNums.Count;
            if (count <= 0) continue;

            totalUnits += count;
            var subtotal = Math.Round(count * rule.RewardPerUnit, 2, MidpointRounding.AwayFromZero);
            totalAmount += subtotal;

            var devQualifier = rule.DeviceCategory is "Gaming" ? " (Gaming)" : (rule.DeviceCategory is "Standard" ? " (Std)" : "");
            summarySegments.Add($"{rule.TaskLabel}{devQualifier}: {count} × {rule.RewardPerUnit:N0} = {subtotal:N0}");

            foreach (var num in ticketNums)
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

    public static bool IsGamingDevice(Device? device, string? complaint, string? deviceSnapshot)
    {
        var text = $"{device?.Brand} {device?.Model} {device?.Notes} {complaint} {deviceSnapshot}".ToUpperInvariant();
        return GamingKeywords.Any(k => text.Contains(k));
    }

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

