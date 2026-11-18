using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.ServiceTickets;

public sealed record TicketBillingInput(
    decimal WorkItemsSubtotal,
    decimal ConsumedPartsSubtotal,
    decimal DiscountAmount,
    decimal? ExistingTotalAmount,
    bool IsTotalManuallyOverridden,
    bool ForceFormulaWhenZero,
    decimal TotalPayments,
    decimal TotalRefunds,
    PaymentStatus CurrentPaymentStatus);

public sealed record TicketBillingCalculation(
    decimal CalculatedTotalAmount,
    decimal? TotalAmount,
    decimal NetPaid,
    decimal RemainingBalance,
    decimal OverpaidAmount,
    PaymentStatus PaymentStatus);

public sealed record TicketBillingSnapshot(
    decimal WorkItemsSubtotal,
    decimal ConsumedPartsSubtotal,
    decimal DiscountAmount,
    decimal CalculatedTotalAmount,
    decimal? TotalAmount,
    decimal AmountPaid,
    decimal RemainingBalance,
    decimal OverpaidAmount,
    PaymentStatus PreviousStatus,
    PaymentStatus PaymentStatus,
    bool IsTotalManuallyOverridden);

public static class TicketBillingCalculator
{
    public static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal ComputeLineTotal(decimal quantity, decimal unitPrice) => RoundMoney(quantity * unitPrice);

    public static TicketBillingCalculation Calculate(TicketBillingInput input)
    {
        var discount = Math.Max(0m, RoundMoney(input.DiscountAmount));
        var calculatedTotal = Math.Max(0m, RoundMoney(input.WorkItemsSubtotal + input.ConsumedPartsSubtotal - discount));
        var hasFormulaValues = input.WorkItemsSubtotal > 0m || input.ConsumedPartsSubtotal > 0m || discount > 0m;

        decimal? effectiveTotal = input.IsTotalManuallyOverridden
            ? input.ExistingTotalAmount.HasValue ? Math.Max(0m, RoundMoney(input.ExistingTotalAmount.Value)) : null
            : (hasFormulaValues || input.ForceFormulaWhenZero
                ? calculatedTotal
                : (input.ExistingTotalAmount.HasValue ? Math.Max(0m, RoundMoney(input.ExistingTotalAmount.Value)) : null));

        var netPaid = Math.Max(0m, RoundMoney(input.TotalPayments - input.TotalRefunds));
        var remainingBalance = effectiveTotal.HasValue
            ? Math.Max(0m, RoundMoney(effectiveTotal.Value - netPaid))
            : 0m;
        var overpaidAmount = effectiveTotal.HasValue
            ? Math.Max(0m, RoundMoney(netPaid - effectiveTotal.Value))
            : 0m;

        var paymentStatus = DerivePaymentStatus(input.CurrentPaymentStatus, effectiveTotal, netPaid);

        return new TicketBillingCalculation(
            calculatedTotal,
            effectiveTotal,
            netPaid,
            remainingBalance,
            overpaidAmount,
            paymentStatus);
    }

    public static PaymentStatus DerivePaymentStatus(PaymentStatus currentStatus, decimal? effectiveTotal, decimal netPaid)
    {
        if (currentStatus == PaymentStatus.Waived)
        {
            return PaymentStatus.Waived;
        }

        if (netPaid <= 0m)
        {
            return PaymentStatus.Unpaid;
        }

        if (effectiveTotal.HasValue && netPaid >= effectiveTotal.Value)
        {
            return PaymentStatus.Paid;
        }

        return PaymentStatus.Partial;
    }
}

public static class TicketBillingService
{
    public static async Task<TicketBillingSnapshot> GetBillingSnapshotAsync(
        AppDbContext context,
        int shopId,
        ServiceTicket ticket,
        CancellationToken cancellationToken = default)
    {
        return await ComputeSnapshotAsync(context, shopId, ticket, forceFormulaWhenZero: false, cancellationToken);
    }

    public static async Task<TicketBillingSnapshot> RecalculateTicketBillingAsync(
        AppDbContext context,
        int shopId,
        ServiceTicket ticket,
        int? actorUserId,
        string? reasonNote = null,
        bool forceFormulaWhenZero = false,
        bool logStatusTransitionHistory = true,
        CancellationToken cancellationToken = default)
    {
        var previousStatus = ticket.PaymentStatus;
        var snapshot = await ComputeSnapshotAsync(context, shopId, ticket, forceFormulaWhenZero, cancellationToken);

        ticket.TotalAmount = snapshot.TotalAmount;
        ticket.AmountPaid = snapshot.AmountPaid;
        ticket.PaymentStatus = snapshot.PaymentStatus;

        if (logStatusTransitionHistory && previousStatus != snapshot.PaymentStatus)
        {
            var reasonPrefix = string.IsNullOrWhiteSpace(reasonNote) ? string.Empty : $"{reasonNote.Trim()}: ";
            var totalDisplay = snapshot.TotalAmount?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
            var paidDisplay = snapshot.AmountPaid.ToString("0.##", CultureInfo.InvariantCulture);

            context.ServiceTicketHistories.Add(new ServiceTicketHistory
            {
                ShopId = shopId,
                ServiceTicketId = ticket.Id,
                ActorUserId = actorUserId,
                EventType = TicketHistoryEventType.PaymentUpdated,
                Reason = string.IsNullOrWhiteSpace(reasonNote) ? null : reasonNote.Trim(),
                Note = $"{reasonPrefix}Payment status changed from {previousStatus} to {snapshot.PaymentStatus} (Total: {totalDisplay}, Paid: {paidDisplay}).",
                IsPublicVisible = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        return snapshot with { PreviousStatus = previousStatus };
    }

    private static async Task<TicketBillingSnapshot> ComputeSnapshotAsync(
        AppDbContext context,
        int shopId,
        ServiceTicket ticket,
        bool forceFormulaWhenZero,
        CancellationToken cancellationToken)
    {
        var approvedWorkItems = await context.ServiceTicketWorkItems
            .AsNoTracking()
            .Where(item => item.ShopId == shopId && item.ServiceTicketId == ticket.Id && item.Status == WorkItemStatus.Approved)
            .Select(item => new { item.Quantity, item.UnitPrice })
            .ToListAsync(cancellationToken);

        var workItemsSubtotal = TicketBillingCalculator.RoundMoney(
            approvedWorkItems.Sum(item => TicketBillingCalculator.ComputeLineTotal(item.Quantity, item.UnitPrice)));

        var inventoryUsages = await context.TicketInventoryUsages
            .AsNoTracking()
            .Where(usage => usage.ShopId == shopId && usage.ServiceTicketId == ticket.Id)
            .Select(usage => new
            {
                usage.ConsumedQuantity,
                usage.ReturnedQuantity,
                usage.UnitPriceSnapshot,
                CurrentPrice = usage.InventoryItem.FixedSalePrice > 0
                    ? usage.InventoryItem.FixedSalePrice
                    : usage.InventoryItem.SalePrice
            })
            .ToListAsync(cancellationToken);

        var consumedPartsSubtotal = TicketBillingCalculator.RoundMoney(
            inventoryUsages.Sum(usage =>
            {
                var netQuantity = Math.Max(0m, usage.ConsumedQuantity - usage.ReturnedQuantity);
                if (netQuantity <= 0m) return 0m;
                var unitPrice = usage.UnitPriceSnapshot ?? usage.CurrentPrice;
                return TicketBillingCalculator.ComputeLineTotal(netQuantity, unitPrice);
            }));

        var ledgerEntries = await context.PaymentRecords
            .AsNoTracking()
            .Where(record => record.ShopId == shopId && record.ServiceTicketId == ticket.Id && !record.IsVoided)
            .Select(record => new { record.EntryType, record.TransactionAmount })
            .ToListAsync(cancellationToken);

        var totalPayments = ledgerEntries
            .Where(entry => entry.EntryType == PaymentEntryType.Payment)
            .Sum(entry => entry.TransactionAmount);
        var totalRefunds = ledgerEntries
            .Where(entry => entry.EntryType == PaymentEntryType.Refund)
            .Sum(entry => entry.TransactionAmount);

        var calculation = TicketBillingCalculator.Calculate(new TicketBillingInput(
            WorkItemsSubtotal: workItemsSubtotal,
            ConsumedPartsSubtotal: consumedPartsSubtotal,
            DiscountAmount: ticket.DiscountAmount,
            ExistingTotalAmount: ticket.TotalAmount,
            IsTotalManuallyOverridden: ticket.IsTotalManuallyOverridden,
            ForceFormulaWhenZero: forceFormulaWhenZero,
            TotalPayments: totalPayments,
            TotalRefunds: totalRefunds,
            CurrentPaymentStatus: ticket.PaymentStatus));

        return new TicketBillingSnapshot(
            workItemsSubtotal,
            consumedPartsSubtotal,
            ticket.DiscountAmount,
            calculation.CalculatedTotalAmount,
            calculation.TotalAmount,
            calculation.NetPaid,
            calculation.RemainingBalance,
            calculation.OverpaidAmount,
            ticket.PaymentStatus,
            calculation.PaymentStatus,
            ticket.IsTotalManuallyOverridden);
    }
}
