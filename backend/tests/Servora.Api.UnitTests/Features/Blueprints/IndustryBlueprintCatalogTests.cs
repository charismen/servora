using FluentAssertions;
using Servora.Api.Features.Blueprints;
using Servora.Api.Features.Shops;
using Xunit;

namespace Servora.Api.UnitTests.Features.Blueprints;

public class IndustryBlueprintCatalogTests
{
    [Fact]
    public void Catalog_ContainsAllSixRequiredBlueprints()
    {
        var keys = IndustryBlueprintCatalog.All.Select(b => b.Key).ToArray();

        keys.Should().Contain([
            "it_computer",
            "mobile_phone",
            "automotive_vehicle",
            "hvac_ac",
            "facility_internal",
            "custom"
        ]);
        keys.Should().HaveCount(6);
    }

    [Theory]
    [InlineData("it_computer", OperatingMode.Commercial, "Device", "Perangkat")]
    [InlineData("mobile_phone", OperatingMode.Commercial, "Device", "Perangkat")]
    [InlineData("automotive_vehicle", OperatingMode.Commercial, "Vehicle", "Kendaraan")]
    [InlineData("hvac_ac", OperatingMode.Commercial, "AC Unit", "Unit AC")]
    [InlineData("facility_internal", OperatingMode.InternalMaintenance, "Equipment", "Peralatan")]
    [InlineData("custom", OperatingMode.Hybrid, "Asset", "Aset")]
    public void Blueprint_HasExpectedModeTerminologyAndLifecycleStatuses(
        string key,
        OperatingMode expectedMode,
        string expectedEnAsset,
        string expectedIdAsset)
    {
        var blueprint = IndustryBlueprintCatalog.GetByKey(key);

        blueprint.Key.Should().Be(key);
        blueprint.RecommendedMode.Should().Be(expectedMode);
        blueprint.Terminology.En.AssetSingular.Should().Be(expectedEnAsset);
        blueprint.Terminology.Id.AssetSingular.Should().Be(expectedIdAsset);

        blueprint.Statuses.Should().ContainSingle(s => s.IsInitial);
        blueprint.Statuses.Should().ContainSingle(s => s.IsFinal);
        blueprint.Statuses.Should().ContainSingle(s => s.IsCancelled);
    }

    [Fact]
    public void TerminologySerialization_RoundTripsAndFallsBackCleanly()
    {
        var original = IndustryBlueprintCatalog.GetByKey("automotive_vehicle").Terminology;
        var json = IndustryBlueprintCatalog.SerializeTerminology(original);

        var restored = IndustryBlueprintCatalog.DeserializeTerminology(json, "it_computer");
        restored.En.AssetSingular.Should().Be("Vehicle");
        restored.Id.TicketSingular.Should().Be("Perintah Kerja");

        var fallback = IndustryBlueprintCatalog.DeserializeTerminology("invalid-json", "hvac_ac");
        fallback.En.AssetSingular.Should().Be("AC Unit");
    }
}
