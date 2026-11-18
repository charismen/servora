using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Servora.Api.Features.Settings;
using Xunit;

namespace Servora.Api.IntegrationTests.Features.Settings;

public class ShopProfileSettingsTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly HttpClient _client;

    public ShopProfileSettingsTests(IntegrationTestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateShopProfileSettings_WithTicketMinDigitsFour_ShouldPersist()
    {
        var currentResponse = await _client.GetAsync("/api/v1/settings/shop-profile");
        currentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var current = await currentResponse.Content.ReadFromJsonAsync<ShopProfileSettings>();
        current.Should().NotBeNull();

        var updateResponse = await _client.PutAsJsonAsync("/api/v1/settings/shop-profile", current! with
        {
            TicketMinDigits = 4
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await updateResponse.Content.ReadFromJsonAsync<ShopProfileSettings>();
        updated.Should().NotBeNull();
        updated!.TicketMinDigits.Should().Be(4);

        var verifyResponse = await _client.GetAsync("/api/v1/settings/shop-profile");
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var verify = await verifyResponse.Content.ReadFromJsonAsync<ShopProfileSettings>();
        verify!.TicketMinDigits.Should().Be(4);
    }

    [Fact]
    public async Task UpdateShopProfileSettings_WithStaleUpdatedAt_ShouldReturnConflict()
    {
        var current = await _client.GetFromJsonAsync<ShopProfileSettings>("/api/v1/settings/shop-profile");
        current.Should().NotBeNull();

        var firstValue = current!.TicketMinDigits == 4 ? 5 : 4;
        var first = await _client.PutAsJsonAsync("/api/v1/settings/shop-profile", current with
        {
            TicketMinDigits = firstValue
        });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var loaded = await _client.GetFromJsonAsync<ShopProfileSettings>("/api/v1/settings/shop-profile");
        loaded.Should().NotBeNull();
        loaded!.UpdatedAt.Should().NotBeNull();

        var second = await _client.PutAsJsonAsync("/api/v1/settings/shop-profile", loaded with
        {
            TicketMinDigits = firstValue == 4 ? 5 : 4
        });
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var stale = await _client.PutAsJsonAsync("/api/v1/settings/shop-profile", loaded with
        {
            TicketMinDigits = firstValue
        });

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ResetShopProfileSettings_ShouldRestoreDefaults()
    {
        var current = await _client.GetFromJsonAsync<ShopProfileSettings>("/api/v1/settings/shop-profile");
        current.Should().NotBeNull();

        var changed = await _client.PutAsJsonAsync("/api/v1/settings/shop-profile", current! with
        {
            TicketMinDigits = 7,
            CurrencyCode = "USD"
        });
        changed.StatusCode.Should().Be(HttpStatusCode.OK);

        var reset = await _client.PostAsync("/api/v1/settings/shop-profile/reset-defaults", null);
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        var settings = await reset.Content.ReadFromJsonAsync<ShopProfileSettings>();
        settings.Should().NotBeNull();
        settings!.TicketMinDigits.Should().Be(int.Parse(ShopProfileSettingKeys.Defaults[ShopProfileSettingKeys.TicketMinDigits]));
        settings.CurrencyCode.Should().Be(ShopProfileSettingKeys.Defaults[ShopProfileSettingKeys.CurrencyCode]);
    }

    [Fact]
    public async Task UploadShopLogo_ShouldPersistLogoUrl()
    {
        using var multipart = new MultipartFormDataContent();
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(file, "file", "logo.png");

        var response = await _client.PostAsync("/api/v1/settings/shop-profile/logo", multipart);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var settings = await response.Content.ReadFromJsonAsync<ShopProfileSettings>();
        settings.Should().NotBeNull();
        settings!.LogoUrl.Should().StartWith("/uploads/shop-logos/");
        var logoUrl = settings.LogoUrl!;

        var verify = await _client.GetFromJsonAsync<ShopProfileSettings>("/api/v1/settings/shop-profile");
        verify!.LogoUrl.Should().Be(logoUrl);

        var path = Path.Combine(Directory.GetCurrentDirectory(), logoUrl.TrimStart('/'));
        if (File.Exists(path)) File.Delete(path);
    }
}
