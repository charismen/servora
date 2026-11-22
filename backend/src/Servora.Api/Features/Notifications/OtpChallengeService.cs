using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Notifications;

public static class OtpCodeHelper
{
    public static string Generate6DigitCode()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string GenerateToken(int byteCount = 24)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static string HashCode(string code, string salt)
    {
        var normalized = new string((code ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{normalized}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool VerifyCode(string inputCode, string salt, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(inputCode) || string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        var actualHash = HashCode(inputCode, salt);
        var a = Encoding.UTF8.GetBytes(actualHash);
        var b = Encoding.UTF8.GetBytes(expectedHash.ToLowerInvariant());
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "***";
        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 1) return "***" + (atIndex >= 0 ? trimmed[atIndex..] : string.Empty);
        var local = trimmed[..atIndex];
        var domain = trimmed[atIndex..];
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}•••{domain}";
    }

    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "••••";
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length <= 4) return $"••••{digits}";
        var prefix = digits.StartsWith("62", StringComparison.Ordinal) ? "+62 " : string.Empty;
        return $"{prefix}••••{digits[^4..]}";
    }
}

public sealed record OtpVerifyResult(
    bool Succeeded,
    OtpVerificationChallenge? Challenge,
    string? ErrorMessage);

public sealed class OtpChallengeService
{
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private readonly AppDbContext _context;

    public OtpChallengeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(bool OnCooldown, int RetryAfterSeconds)> CheckCooldownAsync(
        OtpPurpose purpose,
        int? userId,
        int? serviceTicketId,
        CancellationToken cancellationToken)
    {
        var query = _context.OtpVerificationChallenges.AsNoTracking()
            .Where(x => x.Purpose == purpose);

        if (userId.HasValue)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }
        else if (serviceTicketId.HasValue)
        {
            query = query.Where(x => x.ServiceTicketId == serviceTicketId.Value);
        }
        else
        {
            return (false, 0);
        }

        var latest = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTimeOffset?)x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (!latest.HasValue) return (false, 0);

        var elapsed = DateTimeOffset.UtcNow - latest.Value;
        if (elapsed < ResendCooldown)
        {
            return (true, Math.Max(1, (int)Math.Ceiling((ResendCooldown - elapsed).TotalSeconds)));
        }

        return (false, 0);
    }

    public async Task<(OtpVerificationChallenge Challenge, string PlainCode)> CreateChallengeAsync(
        int? shopId,
        int? userId,
        int? serviceTicketId,
        OtpPurpose purpose,
        NotificationChannel channel,
        string maskedDestination,
        TimeSpan ttl,
        int maxAttempts = 5,
        CancellationToken cancellationToken = default)
    {
        var plainCode = OtpCodeHelper.Generate6DigitCode();
        var salt = OtpCodeHelper.GenerateToken(16);
        var challenge = new OtpVerificationChallenge
        {
            ShopId = shopId,
            UserId = userId,
            ServiceTicketId = serviceTicketId,
            Purpose = purpose,
            Channel = channel,
            ChallengeToken = OtpCodeHelper.GenerateToken(24),
            CodeSalt = salt,
            CodeHash = OtpCodeHelper.HashCode(plainCode, salt),
            MaskedDestination = maskedDestination.Length > 120 ? maskedDestination[..120] : maskedDestination,
            AttemptCount = 0,
            MaxAttempts = maxAttempts,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.OtpVerificationChallenges.Add(challenge);
        await _context.SaveChangesAsync(cancellationToken);
        return (challenge, plainCode);
    }

    public async Task<OtpVerifyResult> VerifyChallengeAsync(
        string challengeToken,
        string code,
        OtpPurpose expectedPurpose,
        bool verifyCodeHash = true,
        CancellationToken cancellationToken = default)
    {
        var challenge = await _context.OtpVerificationChallenges
            .FirstOrDefaultAsync(x => x.ChallengeToken == challengeToken && x.Purpose == expectedPurpose, cancellationToken);

        if (challenge is null)
        {
            return new OtpVerifyResult(false, null, "Invalid or expired verification session.");
        }

        if (challenge.ConsumedAt.HasValue)
        {
            return new OtpVerifyResult(false, challenge, "This verification code has already been used.");
        }

        if (challenge.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return new OtpVerifyResult(false, challenge, "This verification code has expired. Please request a new code.");
        }

        if (challenge.AttemptCount >= challenge.MaxAttempts)
        {
            return new OtpVerifyResult(false, challenge, "Too many failed attempts. Please request a new code.");
        }

        challenge.AttemptCount += 1;

        if (verifyCodeHash && !OtpCodeHelper.VerifyCode(code, challenge.CodeSalt, challenge.CodeHash))
        {
            await _context.SaveChangesAsync(cancellationToken);
            return new OtpVerifyResult(false, challenge, "Invalid verification code.");
        }

        challenge.ConsumedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new OtpVerifyResult(true, challenge, null);
    }

    public async Task<OtpVerificationChallenge?> GetActiveChallengeAsync(
        string challengeToken,
        OtpPurpose expectedPurpose,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _context.OtpVerificationChallenges
            .FirstOrDefaultAsync(
                x => x.ChallengeToken == challengeToken
                    && x.Purpose == expectedPurpose
                    && x.ConsumedAt == null
                    && x.ExpiresAt > now,
                cancellationToken);
    }
}
