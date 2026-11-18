using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Servora.Api.Features.Settings;

namespace Servora.Api.IntegrationTests.Features.Settings;

public class PrintTemplateSettingsTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly HttpClient _client;

    public PrintTemplateSettingsTests(IntegrationTestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdatePrintTemplateSettings_ShouldPersist()
    {
        var terms = $"Terms {Guid.NewGuid():N}";

        var update = await _client.PutAsJsonAsync("/api/v1/settings/print-template", new
        {
            termsAndConditions = terms,
            showPhysicalCondition = false,
            showAccessories = true,
            showPhotoNotes = false,
            showQrCode = true,
            showPhoneNumber = false,
            showPaymentSummary = false
        });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<PrintTemplateSettings>();
        updated.Should().NotBeNull();
        updated!.TermsAndConditions.Should().Be(terms);
        updated.ShowPhysicalCondition.Should().BeFalse();
        updated.ShowPhotoNotes.Should().BeFalse();
        updated.ShowPhoneNumber.Should().BeFalse();
        updated.ShowPaymentSummary.Should().BeFalse();

        var verify = await _client.GetFromJsonAsync<PrintTemplateSettings>("/api/v1/settings/print-template");
        verify.Should().NotBeNull();
        verify!.TermsAndConditions.Should().Be(terms);
        verify.ShowPhysicalCondition.Should().BeFalse();
        verify.ShowPhotoNotes.Should().BeFalse();
        verify.ShowPhoneNumber.Should().BeFalse();
        verify.ShowPaymentSummary.Should().BeFalse();
    }
}
