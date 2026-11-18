using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Payroll.Bonuses;

public sealed record CustomerCancellationParameters(
    decimal FeePerIncident = 25000m,
    bool RequireDiagnosisOrInspectionHistory = true);

public sealed class CustomerCancellationBonusEvaluator(AppDbContext dbContext) : IBonusEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public string BonusTypeCode => BonusTypeCodes.CustomerCancellation;

    public async Task<BonusEvaluationResult> EvaluateAsync(
        BonusEvaluationContext context,
        string parametersJson,
        CancellationToken cancellationToken)
    {
        var parameters = ParseParameters(parametersJson);
        var feePerIncident = Math.Max(0m, parameters.FeePerIncident);

        var candidateTickets = await dbContext.ServiceTickets
            .AsNoTracking()
            .Where(ticket =>
                ticket.ShopId == context.ShopId &&
                ticket.DeletedAt == null &&
                ticket.AssignedTechnicianUserId == context.UserId &&
                ticket.Status.IsCancelled &&
                (ticket.UpdatedAt ?? ticket.CreatedAt) >= context.StartDateTimeUtc &&
                (ticket.UpdatedAt ?? ticket.CreatedAt) < context.EndDateTimeExclusiveUtc)
            .Select(ticket => new
            {
                ticket.Id,
                ticket.TicketNumber,
                HasDiagnosisText = !string.IsNullOrWhiteSpace(ticket.Diagnosis)
                    || !string.IsNullOrWhiteSpace(ticket.InitialDiagnosis)
                    || !string.IsNullOrWhiteSpace(ticket.RepairNotes)
            })
            .ToListAsync(cancellationToken);

        if (candidateTickets.Count == 0)
        {
            return new BonusEvaluationResult(
                BonusTypeCode,
                "Customer Check Cancellation Fee",
                0,
                0,
                feePerIncident,
                0m,
                0m,
                "0 cancelled inspections",
                []);
        }

        List<string> qualifiedTicketNumbers;
        if (!parameters.RequireDiagnosisOrInspectionHistory)
        {
            qualifiedTicketNumbers = candidateTickets.Select(ticket => ticket.TicketNumber).ToList();
        }
        else
        {
            var ticketIdsNeedingHistoryCheck = candidateTickets
                .Where(ticket => !ticket.HasDiagnosisText)
                .Select(ticket => ticket.Id)
                .ToList();

            var inspectedTicketIds = ticketIdsNeedingHistoryCheck.Count == 0
                ? new HashSet<int>()
                : (await dbContext.ServiceTicketHistories
                    .AsNoTracking()
                    .Where(history =>
                        history.ShopId == context.ShopId &&
                        ticketIdsNeedingHistoryCheck.Contains(history.ServiceTicketId) &&
                        (history.ActorUserId == context.UserId || (history.FromStatus != null && !history.FromStatus.IsInitial)))
                    .Select(history => history.ServiceTicketId)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                    .ToHashSet();

            qualifiedTicketNumbers = candidateTickets
                .Where(ticket => ticket.HasDiagnosisText || inspectedTicketIds.Contains(ticket.Id))
                .Select(ticket => ticket.TicketNumber)
                .ToList();
        }

        var qualifiedCount = qualifiedTicketNumbers.Count;
        var totalAmount = Math.Round(qualifiedCount * feePerIncident, 2, MidpointRounding.AwayFromZero);

        return new BonusEvaluationResult(
            BonusTypeCode,
            "Customer Check Cancellation Fee",
            candidateTickets.Count,
            qualifiedCount,
            feePerIncident,
            0m,
            totalAmount,
            $"{qualifiedCount} × {feePerIncident:0}",
            qualifiedTicketNumbers);
    }

    private static CustomerCancellationParameters ParseParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CustomerCancellationParameters();
        }

        try
        {
            return JsonSerializer.Deserialize<CustomerCancellationParameters>(json, JsonOptions)
                ?? new CustomerCancellationParameters();
        }
        catch
        {
            return new CustomerCancellationParameters();
        }
    }
}

