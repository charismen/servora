namespace Servora.Api.Features.Inventory;

public enum InventoryTaxMode
{
    Percentage,
    Multiplier
}

public sealed record InventoryPricingInput(
    decimal PurchasePrice,
    decimal ShippingCost,
    decimal ServiceFee,
    decimal MarginPercent,
    decimal TaxPercent,
    decimal? FixedSalePrice = null,
    decimal RoundingIncrement = 1000m,
    InventoryTaxMode TaxMode = InventoryTaxMode.Percentage);

public sealed record InventoryPricingResult(
    decimal Hpp,
    decimal MarginAmount,
    decimal NetAmount,
    decimal PriceBeforeTax,
    decimal TaxAmount,
    decimal PriceAfterTax,
    decimal FixedSalePrice);

public static class InventoryPricingCalculator
{
    public static decimal NormalizeTaxPercent(decimal value, InventoryTaxMode mode)
    {
        if (mode == InventoryTaxMode.Multiplier)
        {
            return Math.Round((value - 1m) * 100m, 4, MidpointRounding.AwayFromZero);
        }

        return value;
    }

    public static IReadOnlyList<string> Validate(InventoryPricingInput input)
    {
        var errors = new List<string>();
        if (input.PurchasePrice < 0) errors.Add("Purchase price cannot be negative.");
        if (input.ShippingCost < 0) errors.Add("Shipping cost cannot be negative.");
        if (input.ServiceFee < 0) errors.Add("Service fee cannot be negative.");
        if (input.MarginPercent < 0) errors.Add("Margin percent cannot be negative.");
        if (input.TaxMode == InventoryTaxMode.Multiplier)
        {
            if (input.TaxPercent < 1m) errors.Add("Tax multiplier must be at least 1.00.");
        }
        else if (input.TaxPercent < 0)
        {
            errors.Add("Tax percent cannot be negative.");
        }
        if (input.FixedSalePrice < 0) errors.Add("Fixed sale price cannot be negative.");
        if (input.RoundingIncrement <= 0) errors.Add("Rounding increment must be greater than zero.");
        return errors;
    }

    public static InventoryPricingResult Calculate(InventoryPricingInput input)
    {
        var errors = Validate(input);
        if (errors.Count > 0) throw new ArgumentOutOfRangeException(nameof(input), string.Join(" ", errors));

        var effectiveTaxPercent = NormalizeTaxPercent(input.TaxPercent, input.TaxMode);
        var hpp = input.PurchasePrice + input.ShippingCost;
        var marginAmount = input.PurchasePrice * input.MarginPercent / 100m;
        var netAmount = input.ServiceFee + marginAmount;
        var priceBeforeTax = hpp + netAmount;
        var taxAmount = priceBeforeTax * effectiveTaxPercent / 100m;
        var priceAfterTax = priceBeforeTax + taxAmount;
        var fixedSalePrice = input.FixedSalePrice ?? Math.Round(priceAfterTax / input.RoundingIncrement, MidpointRounding.AwayFromZero) * input.RoundingIncrement;

        return new InventoryPricingResult(hpp, marginAmount, netAmount, priceBeforeTax, taxAmount, priceAfterTax, fixedSalePrice);
    }
}
