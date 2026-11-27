using FluentAssertions;
using Servora.Api.Features.Inventory;

namespace Servora.Api.UnitTests.Features.Inventory;

public class InventorySkuGeneratorTests
{
    [Theory]
    [InlineData(null, "ITM")]
    [InlineData("", "ITM")]
    [InlineData("   ", "ITM")]
    [InlineData("Battery", "BAT")]
    [InlineData("RAM", "RAM")]
    [InlineData("SSD", "SSD")]
    [InlineData("Power Supply", "PS")]
    [InlineData("Solid State Drive", "SSD")]
    [InlineData("Thermal Paste & Pads", "TPP")]
    public void BuildPrefix_DerivesExpectedShortUppercasePrefix(string? categoryName, string expectedPrefix)
    {
        InventorySkuGenerator.BuildPrefix(categoryName).Should().Be(expectedPrefix);
    }

    [Fact]
    public void BuildNextSku_StartsAt001WhenNoMatchingSkusExist()
    {
        var (sku, prefix, sequence) = InventorySkuGenerator.BuildNextSku("BAT", ["RAM-001", "SSD-005"]);

        sku.Should().Be("BAT-001");
        prefix.Should().Be("BAT");
        sequence.Should().Be(1);
    }

    [Fact]
    public void BuildNextSku_IncrementsPastHighestSequenceCaseInsensitively()
    {
        var (sku, prefix, sequence) = InventorySkuGenerator.BuildNextSku("bat", ["BAT-001", "bat-009", "BAT-MISC", "BAT-004"]);

        sku.Should().Be("BAT-010");
        prefix.Should().Be("BAT");
        sequence.Should().Be(10);
    }
}
