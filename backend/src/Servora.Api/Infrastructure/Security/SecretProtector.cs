using System.Security.Cryptography;
using System.Text;

namespace Servora.Api.Infrastructure.Security;

public interface ISecretProtector
{
    bool IsEncryptionKeyConfigured { get; }
    bool CanStoreSecrets { get; }
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const string Prefix = "v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] DevFallbackKey = SHA256.HashData(
        Encoding.UTF8.GetBytes("servora-dev-secret-encryption-key-change-in-production"));

    private readonly byte[]? _key;
    private readonly bool _isProduction;

    public bool IsEncryptionKeyConfigured { get; }
    public bool CanStoreSecrets => _key is not null;

    public AesGcmSecretProtector(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<AesGcmSecretProtector> logger)
    {
        _isProduction = environment.IsProduction();
        var configuredKey = configuration["Security:SecretEncryptionKey"]?.Trim();

        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            _key = DeriveKey(configuredKey);
            IsEncryptionKeyConfigured = true;
            return;
        }

        IsEncryptionKeyConfigured = false;
        if (_isProduction)
        {
            _key = null;
            logger.LogWarning(
                "Security:SecretEncryptionKey is not configured in Production. Saving encrypted payment gateway credentials will be rejected until a key is provided.");
        }
        else
        {
            _key = DevFallbackKey;
            logger.LogWarning(
                "Security:SecretEncryptionKey is not configured. Using a deterministic non-production fallback key for {EnvironmentName}.",
                environment.EnvironmentName);
        }
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            throw new ArgumentException("Plaintext secret cannot be empty.", nameof(plaintext));
        }

        var key = EnsureKey();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        return $"{Prefix}{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(tag)}:{Convert.ToBase64String(ciphertext)}";
    }

    public string Unprotect(string ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext) || !ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new CryptographicException("Invalid protected secret format.");
        }

        var key = EnsureKey();
        var parts = ciphertext[Prefix.Length..].Split(':');
        if (parts.Length != 3)
        {
            throw new CryptographicException("Malformed protected secret payload.");
        }

        var nonce = Convert.FromBase64String(parts[0]);
        var tag = Convert.FromBase64String(parts[1]);
        var cipherBytes = Convert.FromBase64String(parts[2]);

        if (nonce.Length != NonceSize || tag.Length != TagSize)
        {
            throw new CryptographicException("Invalid cryptographic parameter lengths.");
        }

        var plaintextBytes = new byte[cipherBytes.Length];
        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plaintextBytes);
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private byte[] EnsureKey()
    {
        if (_key is not null)
        {
            return _key;
        }

        throw new InvalidOperationException(
            "Security:SecretEncryptionKey must be configured in Production before storing or decrypting payment gateway credentials.");
    }

    private static byte[] DeriveKey(string configuredKey)
    {
        try
        {
            var raw = Convert.FromBase64String(configuredKey);
            if (raw.Length == 32)
            {
                return raw;
            }
        }
        catch (FormatException)
        {
            // Fall through and hash passphrase into a 32-byte AES-256 key.
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
    }
}
