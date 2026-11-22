using FluentAssertions;
using Servora.Api.Features.Payments;
using Xunit;

namespace Servora.Api.UnitTests.Features.Payments;

public class XenditGatewayAdapterTests
{
    [Fact]
    public void VerifyAndParseWebhook_AcceptsValidPaidInvoiceCallback()
    {
        var payload = """
        {
          "id": "inv_xnd_001",
          "external_id": "srv-1-10-xyz",
          "status": "PAID",
          "payment_method": "QR_CODE",
          "payment_channel": "QRIS",
          "paid_at": "2026-11-22T10:15:00Z",
          "updated": "2026-11-22T10:15:01Z"
        }
        """;

        var result = XenditGatewayAdapter.VerifyAndParseWebhook(
            configuredWebhookSecret: "xnd_cb_secret_token",
            callbackTokenHeader: "xnd_cb_secret_token",
            webhookIdHeader: "whk_12345",
            rawBody: payload);

        result.IsValid.Should().BeTrue();
        result.ProviderEventId.Should().Be("whk_12345");
        result.ProviderCheckoutId.Should().Be("inv_xnd_001");
        result.ExternalReference.Should().Be("srv-1-10-xyz");
        result.MappedStatus.Should().Be(PaymentCheckoutStatus.Paid);
        result.ProviderPaymentMethod.Should().Be("QR_CODE - QRIS");
        result.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void VerifyAndParseWebhook_RejectsWrongCallbackToken()
    {
        var payload = """{"id":"inv_1","external_id":"ref_1","status":"PAID"}""";

        var result = XenditGatewayAdapter.VerifyAndParseWebhook(
            "expected-token",
            "wrong-token",
            null,
            payload);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("x-callback-token");
    }

    [Theory]
    [InlineData("PAID", PaymentCheckoutStatus.Paid)]
    [InlineData("SETTLED", PaymentCheckoutStatus.Paid)]
    [InlineData("EXPIRED", PaymentCheckoutStatus.Expired)]
    [InlineData("PENDING", PaymentCheckoutStatus.Pending)]
    [InlineData("FAILED", PaymentCheckoutStatus.Failed)]
    public void MapXenditStatus_NormalizesStatuses(string rawStatus, PaymentCheckoutStatus expected)
    {
        XenditGatewayAdapter.MapXenditStatus(rawStatus).Should().Be(expected);
    }
}
