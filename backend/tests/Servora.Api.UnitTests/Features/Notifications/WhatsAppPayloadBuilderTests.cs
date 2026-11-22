using System.Text.Json;
using FluentAssertions;
using Servora.Api.Features.Notifications;
using Xunit;

namespace Servora.Api.UnitTests.Features.Notifications;

public class WhatsAppPayloadBuilderTests
{
    [Fact]
    public void BuildHttpRequest_MetaCloud_UsesGraphEndpointAndBearerToken()
    {
        var config = new WhatsAppConfig(
            WhatsAppProviderType.MetaCloud,
            ApiUrl: null,
            PhoneNumberId: "9988776655",
            ApiToken: "EAAG_TEST_TOKEN",
            AuthHeaderName: null,
            CustomPayloadTemplate: null);

        var built = HttpWhatsAppSender.BuildHttpRequest(config, "0812-3456-7890", "Ticket ready!", "ticket.status_changed");

        built.Url.Should().Contain("/9988776655/messages");
        built.AuthHeaderName.Should().Be("Authorization");
        built.AuthHeaderValue.Should().Be("Bearer EAAG_TEST_TOKEN");

        using var doc = JsonDocument.Parse(built.JsonBody);
        doc.RootElement.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        doc.RootElement.GetProperty("to").GetString().Should().Be("6281234567890");
        doc.RootElement.GetProperty("text").GetProperty("body").GetString().Should().Be("Ticket ready!");
    }

    [Fact]
    public void BuildHttpRequest_Fonnte_UsesDefaultEndpointAndRawTokenHeader()
    {
        var config = new WhatsAppConfig(
            WhatsAppProviderType.Fonnte,
            ApiUrl: null,
            PhoneNumberId: null,
            ApiToken: "FONNTE_SECRET_KEY",
            AuthHeaderName: null,
            CustomPayloadTemplate: null);

        var built = HttpWhatsAppSender.BuildHttpRequest(config, "+62 813-9999-0000", "Quote ready", "approval.requested");

        built.Url.Should().Be("https://api.fonnte.com/send");
        built.AuthHeaderName.Should().Be("Authorization");
        built.AuthHeaderValue.Should().Be("FONNTE_SECRET_KEY");

        using var doc = JsonDocument.Parse(built.JsonBody);
        doc.RootElement.GetProperty("target").GetString().Should().Be("6281399990000");
        doc.RootElement.GetProperty("message").GetString().Should().Be("Quote ready");
    }

    [Fact]
    public void BuildHttpRequest_Waha_FormatsChatIdAndXApiKey()
    {
        var config = new WhatsAppConfig(
            WhatsAppProviderType.Waha,
            ApiUrl: "https://waha.example.com",
            PhoneNumberId: "default",
            ApiToken: "waha_key_123",
            AuthHeaderName: null,
            CustomPayloadTemplate: null);

        var built = HttpWhatsAppSender.BuildHttpRequest(config, "628111222333", "Technician en route", "dispatch.en_route");

        built.Url.Should().Be("https://waha.example.com/api/sendText");
        built.AuthHeaderName.Should().Be("X-Api-Key");
        built.AuthHeaderValue.Should().Be("waha_key_123");

        using var doc = JsonDocument.Parse(built.JsonBody);
        doc.RootElement.GetProperty("chatId").GetString().Should().Be("628111222333@c.us");
        doc.RootElement.GetProperty("text").GetString().Should().Be("Technician en route");
    }

    [Fact]
    public void BuildHttpRequest_CustomHttp_EscapesJsonMessageSafely()
    {
        var config = new WhatsAppConfig(
            WhatsAppProviderType.CustomHttp,
            ApiUrl: "https://gateway.example.com/v1/send",
            PhoneNumberId: null,
            ApiToken: "custom-secret",
            AuthHeaderName: "X-Custom-Auth",
            CustomPayloadTemplate: "{\"to\":\"{{phone}}\",\"text\":\"{{message}}\",\"kind\":\"{{event}}\"}");

        var built = HttpWhatsAppSender.BuildHttpRequest(config, "0812000111", "Line 1\n\"Quoted\" item", "ticket.created");

        built.Url.Should().Be("https://gateway.example.com/v1/send");
        built.AuthHeaderName.Should().Be("X-Custom-Auth");
        built.AuthHeaderValue.Should().Be("custom-secret");

        using var doc = JsonDocument.Parse(built.JsonBody);
        doc.RootElement.GetProperty("to").GetString().Should().Be("62812000111");
        doc.RootElement.GetProperty("text").GetString().Should().Be("Line 1\n\"Quoted\" item");
        doc.RootElement.GetProperty("kind").GetString().Should().Be("ticket.created");
    }
}
