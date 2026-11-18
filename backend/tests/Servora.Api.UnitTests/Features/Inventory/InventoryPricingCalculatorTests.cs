using FluentAssertions;
using Servora.Api.Features.Inventory;

namespace Servora.Api.UnitTests.Features.Inventory;

public class InventoryPricingCalculatorTests
{
    [Fact]
    public void Calculate_UsesExcelStyleFormulaAndRoundsFixedSalePrice()
    {
        var result = InventoryPricingCalculator.Calculate(new InventoryPricingInput(
            PurchasePrice: 1_000_000m,
            ShippingCost: 50_000m,
            ServiceFee: 100_000m,
            MarginPercent: 20m,
            TaxPercent: 11m));

        result.Hpp.Should().Be(1_050_000m);
        result.MarginAmount.Should().Be(200_000m);
        result.NetAmount.Should().Be(300_000m);
        result.PriceBeforeTax.Should().Be(1_350_000m);
        result.TaxAmount.Should().Be(148_500m);
        result.PriceAfterTax.Should().Be(1_498_500m);
        result.FixedSalePrice.Should().Be(1_499_000m);
    }

    [Fact]
    public void Calculate_KeepsExplicitFixedSalePriceForExistingApiCompatibility()
    {
        var result = InventoryPricingCalculator.Calculate(new InventoryPricingInput(
            PurchasePrice: 100_000m,
            ShippingCost: 0m,
            ServiceFee: 0m,
            MarginPercent: 0m,
            TaxPercent: 11m,
            FixedSalePrice: 125_000m));

        result.FixedSalePrice.Should().Be(125_000m);
    }

    [Fact]
    public void Calculate_UsesConfiguredRoundingIncrement()
    {
        var result = InventoryPricingCalculator.Calculate(new InventoryPricingInput(
            PurchasePrice: 1_000_000m,
            ShippingCost: 50_000m,
            ServiceFee: 100_000m,
            MarginPercent: 20m,
            TaxPercent: 11m,
            RoundingIncrement: 50_000m));

        result.FixedSalePrice.Should().Be(1_500_000m);
    }

    [Fact]
    public void Validate_ReturnsErrorsForNegativeInputs()
    {
        var errors = InventoryPricingCalculator.Validate(new InventoryPricingInput(
            PurchasePrice: -1m,
            ShippingCost: -1m,
            ServiceFee: -1m,
            MarginPercent: -1m,
            TaxPercent: -1m,
            FixedSalePrice: -1m,
            RoundingIncrement: 0m));

        errors.Should().HaveCount(7);
    }
}
