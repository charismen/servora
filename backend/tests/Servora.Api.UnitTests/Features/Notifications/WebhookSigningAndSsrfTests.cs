using System.Net;
using FluentAssertions;
using Servora.Api.Features.Notifications;
using Xunit;

namespace Servora.Api.UnitTests.Features.Notifications;

public class WebhookSigningAndSsrfTests
{
    [Fact]
    public void ComputeHeader_And_VerifyHeader_RoundTrip_Succeeds()
    {
        var secret = "whsec_test_secret_123456";
        var now = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        var body = """{"event":"ticket.created","id":42}""";

        var header = WebhookSignatureHelper.ComputeHeader(secret, now.ToUnixTimeSeconds(), body);

        header.Should().StartWith($"t={now.ToUnixTimeSeconds()},v1=");
        WebhookSignatureHelper.VerifyHeader(secret, header, body, now.AddSeconds(30)).Should().BeTrue();
    }

    [Fact]
    public void VerifyHeader_Rejects_TamperedBody_WrongSecret_And_ExpiredTimestamp()
    {
        var secret = "whsec_test_secret_123456";
        var now = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        var body = """{"event":"payment.settled","amount":150000}""";
        var header = WebhookSignatureHelper.ComputeHeader(secret, now.ToUnixTimeSeconds(), body);

        WebhookSignatureHelper.VerifyHeader(secret, header, """{"event":"payment.settled","amount":1}""", now).Should().BeFalse();
        WebhookSignatureHelper.VerifyHeader("whsec_wrong_secret", header, body, now).Should().BeFalse();
        WebhookSignatureHelper.VerifyHeader(secret, header, body, now.AddMinutes(10)).Should().BeFalse();
    }

    [Theory]
    [InlineData("http://localhost:8080/hook")]
    [InlineData("http://127.0.0.1/hook")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://10.0.0.5/internal")]
    [InlineData("https://192.168.1.10/webhook")]
    [InlineData("https://172.16.0.1/webhook")]
    [InlineData("https://service.local/hook")]
    public void ValidateStaticUrl_Blocks_Private_Loopback_And_Metadata_Targets(string url)
    {
        var (isValid, error) = WebhookUrlValidator.ValidateStaticUrl(url, isProduction: true, allowPrivateTargets: false);

        isValid.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ValidateStaticUrl_Requires_Https_In_Production()
    {
        var (isValid, error) = WebhookUrlValidator.ValidateStaticUrl("http://hooks.example.com/servora", isProduction: true, allowPrivateTargets: false);

        isValid.Should().BeFalse();
        error.Should().Contain("HTTPS");
    }

    [Fact]
    public void ValidateStaticUrl_Allows_Public_Https_Url()
    {
        var (isValid, error) = WebhookUrlValidator.ValidateStaticUrl("https://hooks.example.com/servora", isProduction: true, allowPrivateTargets: false);

        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("172.20.0.1", true)]
    [InlineData("192.168.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("1.1.1.1", false)]
    public void IsPrivateOrLoopback_Classifies_IpAddresses_Accurately(string ipText, bool expectedPrivate)
    {
        WebhookUrlValidator.IsPrivateOrLoopback(IPAddress.Parse(ipText)).Should().Be(expectedPrivate);
    }
}
