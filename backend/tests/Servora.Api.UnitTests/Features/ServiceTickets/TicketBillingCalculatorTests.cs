using FluentAssertions;
using Servora.Api.Features.ServiceTickets;

namespace Servora.Api.UnitTests.Features.ServiceTickets;

public class TicketBillingCalculatorTests
{
    [Fact]
    public void ComputeLineTotal_RoundsAwayFromZeroToTwoDecimals()
    {
        TicketBillingCalculator.ComputeLineTotal(1.5m, 33.335m).Should().Be(50.00m);
        TicketBillingCalculator.ComputeLineTotal(12.5m, 33.33m).Should().Be(416.63m);
        TicketBillingCalculator.ComputeLineTotal(2m, 150000m).Should().Be(300000m);
    }

    [Fact]
    public void Calculate_ComputesWorkItemsPlusPartsMinusDiscountAndClampsAtZero()
    {
        var result = TicketBillingCalculator.Calculate(new TicketBillingInput(
            WorkItemsSubtotal: 450000m,
            ConsumedPartsSubtotal: 500000m,
            DiscountAmount: 50000m,
            ExistingTotalAmount: null,
            IsTotalManuallyOverridden: false,
            ForceFormulaWhenZero: false,
            TotalPayments: 300000m,
            TotalRefunds: 0m,
            CurrentPaymentStatus: PaymentStatus.Unpaid));

        result.CalculatedTotalAmount.Should().Be(900000m);
        result.TotalAmount.Should().Be(900000m);
        result.NetPaid.Should().Be(300000m);
        result.RemainingBalance.Should().Be(600000m);
        result.OverpaidAmount.Should().Be(0m);
        result.PaymentStatus.Should().Be(PaymentStatus.Partial);

        var hugeDiscount = TicketBillingCalculator.Calculate(new TicketBillingInput(
            WorkItemsSubtotal: 100000m,
            ConsumedPartsSubtotal: 0m,
            DiscountAmount: 250000m,
            ExistingTotalAmount: null,
            IsTotalManuallyOverridden: false,
            ForceFormulaWhenZero: false,
            TotalPayments: 0m,
            TotalRefunds: 0m,
            CurrentPaymentStatus: PaymentStatus.Unpaid));

        hugeDiscount.CalculatedTotalAmount.Should().Be(0m);
        hugeDiscount.TotalAmount.Should().Be(0m);
        hugeDiscount.RemainingBalance.Should().Be(0m);
        hugeDiscount.PaymentStatus.Should().Be(PaymentStatus.Unpaid);
    }

    [Fact]
    public void Calculate_RespectsManualOverrideAndForceFormulaWhenZero()
    {
        var overridden = TicketBillingCalculator.Calculate(new TicketBillingInput(
            WorkItemsSubtotal: 450000m,
            ConsumedPartsSubtotal: 500000m,
            DiscountAmount: 0m,
            ExistingTotalAmount: 800000m,
            IsTotalManuallyOverridden: true,
            ForceFormulaWhenZero: false,
            TotalPayments: 800000m,
            TotalRefunds: 0m,
            CurrentPaymentStatus: PaymentStatus.Partial));

        overridden.CalculatedTotalAmount.Should().Be(950000m);
        overridden.TotalAmount.Should().Be(800000m);
        overridden.RemainingBalance.Should().Be(0m);
        overridden.PaymentStatus.Should().Be(PaymentStatus.Paid);

        var clearedToZero = TicketBillingCalculator.Calculate(new TicketBillingInput(
            WorkItemsSubtotal: 0m,
            ConsumedPartsSubtotal: 0m,
            DiscountAmount: 0m,
            ExistingTotalAmount: 500000m,
            IsTotalManuallyOverridden: false,
            ForceFormulaWhenZero: true,
            TotalPayments: 0m,
            TotalRefunds: 0m,
            CurrentPaymentStatus: PaymentStatus.Unpaid));

        clearedToZero.TotalAmount.Should().Be(0m);

        var keepLegacyTotalWhenEmpty = TicketBillingCalculator.Calculate(new TicketBillingInput(
            WorkItemsSubtotal: 0m,
            ConsumedPartsSubtotal: 0m,
            DiscountAmount: 0m,
            ExistingTotalAmount: 500000m,
            IsTotalManuallyOverridden: false,
            ForceFormulaWhenZero: false,
            TotalPayments: 500000m,
            TotalRefunds: 0m,
            CurrentPaymentStatus: PaymentStatus.Paid));

        keepLegacyTotalWhenEmpty.TotalAmount.Should().Be(500000m);
        keepLegacyTotalWhenEmpty.PaymentStatus.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void Calculate_RevertsPaidToPartialWhenAdditionalApprovedWorkIncreasesTotal()
    {
        var reopened = TicketBillingCalculator.Calculate(new TicketBillingInput(
            WorkItemsSubtotal: 450000m,
            ConsumedPartsSubtotal: 500000m,
            DiscountAmount: 0m,
            ExistingTotalAmount: 350000m,
            IsTotalManuallyOverridden: false,
            ForceFormulaWhenZero: false,
            TotalPayments: 350000m,
            TotalRefunds: 0m,
            CurrentPaymentStatus: PaymentStatus.Paid));

        reopened.TotalAmount.Should().Be(950000m);
        reopened.NetPaid.Should().Be(350000m);
        reopened.RemainingBalance.Should().Be(600000m);
        reopened.OverpaidAmount.Should().Be(0m);
        reopened.PaymentStatus.Should().Be(PaymentStatus.Partial);
    }

    [Fact]
    public void Calculate_ComputesNetPaidAfterVoidedRefundsAndSurfacesOverpayment()
    {
        var overpaid = TicketBillingCalculator.Calculate(new TicketBillingInput(
            WorkItemsSubtotal: 100000m,
            ConsumedPartsSubtotal: 0m,
            DiscountAmount: 0m,
            ExistingTotalAmount: 400000m,
            IsTotalManuallyOverridden: false,
            ForceFormulaWhenZero: true,
            TotalPayments: 400000m,
            TotalRefunds: 50000m,
            CurrentPaymentStatus: PaymentStatus.Paid));

        overpaid.TotalAmount.Should().Be(100000m);
        overpaid.NetPaid.Should().Be(350000m);
        overpaid.RemainingBalance.Should().Be(0m);
        overpaid.OverpaidAmount.Should().Be(250000m);
        overpaid.PaymentStatus.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void Calculate_PreservesWaivedStatusAcrossBillingChanges()
    {
        var waived = TicketBillingCalculator.Calculate(new TicketBillingInput(
            WorkItemsSubtotal: 500000m,
            ConsumedPartsSubtotal: 200000m,
            DiscountAmount: 0m,
            ExistingTotalAmount: 500000m,
            IsTotalManuallyOverridden: false,
            ForceFormulaWhenZero: true,
            TotalPayments: 0m,
            TotalRefunds: 0m,
            CurrentPaymentStatus: PaymentStatus.Waived));

        waived.TotalAmount.Should().Be(700000m);
        waived.PaymentStatus.Should().Be(PaymentStatus.Waived);
    }
}
