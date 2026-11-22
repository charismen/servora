using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Api.Infrastructure.Security;
using Xunit;

namespace Servora.Api.UnitTests.Features.Payments;

public class SecretProtectorTests
{
    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Servora.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static AesGcmSecretProtector CreateProtector(string environmentName, string? key)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:SecretEncryptionKey"] = key
            })
            .Build();

        return new AesGcmSecretProtector(
            config,
            new StubHostEnvironment(environmentName),
            NullLogger<AesGcmSecretProtector>.Instance);
    }

    [Fact]
    public void Protect_And_Unprotect_RoundTrips_WithBase64Key()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var protector = CreateProtector(Environments.Production, key);

        protector.IsEncryptionKeyConfigured.Should().BeTrue();
        protector.CanStoreSecrets.Should().BeTrue();

        var encrypted1 = protector.Protect("xnd_live_secret_123456");
        var encrypted2 = protector.Protect("xnd_live_secret_123456");

        encrypted1.Should().StartWith("v1:");
        encrypted1.Should().NotBe(encrypted2, "each encryption uses a fresh random 12-byte nonce");
        protector.Unprotect(encrypted1).Should().Be("xnd_live_secret_123456");
        protector.Unprotect(encrypted2).Should().Be("xnd_live_secret_123456");
    }

    [Fact]
    public void Protect_And_Unprotect_RoundTrips_WithPassphraseKey()
    {
        var protector = CreateProtector(Environments.Production, "my-long-custom-passphrase-key-for-servora");
        var encrypted = protector.Protect("sk_live_abc123");
        protector.Unprotect(encrypted).Should().Be("sk_live_abc123");
    }

    [Fact]
    public void Unprotect_Rejects_TamperedCiphertext()
    {
        var protector = CreateProtector(Environments.Production, "tamper-test-encryption-key");
        var encrypted = protector.Protect("sensitive-api-key");
        var parts = encrypted.Split(':');
        var bytes = Convert.FromBase64String(parts[3]);
        bytes[0] ^= 0xFF;
        var tampered = $"{parts[0]}:{parts[1]}:{parts[2]}:{Convert.ToBase64String(bytes)}";

        var act = () => protector.Unprotect(tampered);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Production_WithoutKey_RefusesToStoreSecrets()
    {
        var protector = CreateProtector(Environments.Production, null);

        protector.IsEncryptionKeyConfigured.Should().BeFalse();
        protector.CanStoreSecrets.Should().BeFalse();

        var act = () => protector.Protect("sk_live_secret");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Development_WithoutKey_UsesFallbackEncryption()
    {
        var protector = CreateProtector(Environments.Development, null);

        protector.IsEncryptionKeyConfigured.Should().BeFalse();
        protector.CanStoreSecrets.Should().BeTrue();

        var encrypted = protector.Protect("xnd_development_abc");
        protector.Unprotect(encrypted).Should().Be("xnd_development_abc");
    }
}
