using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Servora.Api.Features.Payments;
using Xunit;

namespace Servora.Api.UnitTests.Features.Payments;

public class StripeGatewayAdapterTests
{
    [Theory]
    [InlineData(100, "USD", 10000)]
    [InlineData(49.95, "SGD", 4995)]
    [InlineData(250000, "IDR", 25000000)]
    [InlineData(1500, "JPY", 1500)]
    [InlineData(50000, "KRW", 50000)]
    [InlineData(120000, "VND", 120000)]
    public void ToStripeMinorUnits_HandlesZeroDecimalAndTwoDecimalCurrencies(decimal amount, string currency, long expected)
    {
        StripeGatewayAdapter.ToStripeMinorUnits(amount, currency).Should().Be(expected);
    }

    [Fact]
    public void VerifyAndParseWebhook_AcceptsValidPaidCheckoutCompletedEvent()
    {
        var secret = "whsec_test_1234567890";
        var now = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        var payload = """
        {
          "id": "evt_12345",
          "type": "checkout.session.completed",
          "created": 1795348800,
          "data": {
            "object": {
              "id": "cs_test_999",
              "client_reference_id": "srv-1-42-abc",
              "payment_status": "paid",
              "payment_method_types": ["card"]
            }
          }
        }
        """;
        var header = Sign(secret, now.ToUnixTimeSeconds(), payload);

        var result = StripeGatewayAdapter.VerifyAndParseWebhook(secret, header, payload, now);

        result.IsValid.Should().BeTrue();
        result.ProviderEventId.Should().Be("evt_12345");
        result.ProviderCheckoutId.Should().Be("cs_test_999");
        result.ExternalReference.Should().Be("srv-1-42-abc");
        result.MappedStatus.Should().Be(PaymentCheckoutStatus.Paid);
        result.ProviderPaymentMethod.Should().Be("CARD");
    }

    [Fact]
    public void VerifyAndParseWebhook_RejectsInvalidHmacSignature()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = """{"id":"evt_1","type":"checkout.session.expired","data":{"object":{"id":"cs_1"}}}""";
        var badHeader = Sign("whsec_wrong", now.ToUnixTimeSeconds(), payload);

        var result = StripeGatewayAdapter.VerifyAndParseWebhook("whsec_expected", badHeader, payload, now);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HMAC");
    }

    [Fact]
    public void VerifyAndParseWebhook_RejectsExpiredTimestampReplay()
    {
        var secret = "whsec_replay_test";
        var now = new DateTimeOffset(2026, 11, 22, 12, 10, 0, TimeSpan.Zero);
        var oldTimestamp = now.AddMinutes(-10).ToUnixTimeSeconds();
        var payload = """{"id":"evt_old","type":"checkout.session.expired","data":{"object":{"id":"cs_1"}}}""";
        var header = Sign(secret, oldTimestamp, payload);

        var result = StripeGatewayAdapter.VerifyAndParseWebhook(secret, header, payload, now);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("replay tolerance");
    }

    private static string Sign(string secret, long timestamp, string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}"));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={timestamp},v1={hex}";
    }
}
