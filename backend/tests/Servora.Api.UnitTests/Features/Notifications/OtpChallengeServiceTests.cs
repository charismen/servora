using FluentAssertions;
using Servora.Api.Features.Notifications;
using Xunit;

namespace Servora.Api.UnitTests.Features.Notifications;

public class OtpChallengeServiceTests
{
    [Fact]
    public void Generate6DigitCode_ProducesSixAsciiDigits()
    {
        for (var i = 0; i < 20; i++)
        {
            var code = OtpCodeHelper.Generate6DigitCode();
            code.Should().HaveLength(6);
            code.Should().MatchRegex("^[0-9]{6}$");
        }
    }

    [Fact]
    public void HashCode_And_VerifyCode_Enforces_Salted_Match()
    {
        var salt = OtpCodeHelper.GenerateToken(16);
        var hash = OtpCodeHelper.HashCode("482910", salt);

        hash.Should().NotContain("482910");
        OtpCodeHelper.VerifyCode("482910", salt, hash).Should().BeTrue();
        OtpCodeHelper.VerifyCode("482 910", salt, hash).Should().BeTrue();
        OtpCodeHelper.VerifyCode("482911", salt, hash).Should().BeFalse();
        OtpCodeHelper.VerifyCode("482910", "different-salt", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("alice.smith@example.com", "al•••@example.com")]
    [InlineData("ab@test.io", "a•••@test.io")]
    public void MaskEmail_Masks_LocalPart_Safely(string email, string expected)
    {
        OtpCodeHelper.MaskEmail(email).Should().Be(expected);
    }

    [Theory]
    [InlineData("+62 812-3456-7890", "+62 ••••7890")]
    [InlineData("081234567890", "••••7890")]
    public void MaskPhone_Shows_Only_LastFourDigits(string phone, string expected)
    {
        OtpCodeHelper.MaskPhone(phone).Should().Be(expected);
    }

    [Fact]
    public void EvaluateQuoteOtpOptions_FallsBack_To_PhoneLastFour_When_NoApiChannelUsable()
    {
        var config = new WorkspaceNotificationChannel
        {
            RequireOtpForPublicQuoteApproval = true,
            EmailEnabled = false,
            WhatsAppEnabled = true,
            WhatsAppProvider = WhatsAppProviderType.Manual
        };

        var (requiresOtp, channels) = PublicQuoteApprovalEndpoints.EvaluateQuoteOtpOptions(
            config,
            systemSmtpConfigured: false,
            customerEmail: "cust@example.com",
            customerPhone: "+6281234567890");

        requiresOtp.Should().BeFalse();
        channels.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateQuoteOtpOptions_Returns_Masked_WhatsApp_And_Email_When_Active()
    {
        var config = new WorkspaceNotificationChannel
        {
            RequireOtpForPublicQuoteApproval = true,
            EmailEnabled = true,
            UseSystemSmtp = true,
            WhatsAppEnabled = true,
            WhatsAppProvider = WhatsAppProviderType.Fonnte
        };

        var (requiresOtp, channels) = PublicQuoteApprovalEndpoints.EvaluateQuoteOtpOptions(
            config,
            systemSmtpConfigured: true,
            customerEmail: "customer@example.com",
            customerPhone: "6281234567890");

        requiresOtp.Should().BeTrue();
        channels.Should().HaveCount(2);
        channels.Should().Contain(c => c.Channel == "WhatsApp" && c.MaskedDestination.EndsWith("7890"));
        channels.Should().Contain(c => c.Channel == "Email" && c.MaskedDestination == "cu•••@example.com");
    }
}
