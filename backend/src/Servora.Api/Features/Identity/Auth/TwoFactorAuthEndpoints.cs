using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Identity.Auth;

public static class TwoFactorAuthEndpoints
{
    public sealed record VerifyLogin2FaRequest(string ChallengeToken, string Code, bool UseRecoveryCode = false);
    public sealed record ResendLogin2FaRequest(string ChallengeToken);
    public sealed record ConfirmTotpRequest(string Code);
    public sealed record ConfirmEmailOtpRequest(string ChallengeToken, string Code);
    public sealed record PasswordConfirmRequest(string CurrentPassword);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Step 2 of 2FA login (anonymous, rate-limited)
        app.MapPost("/auth/login/verify-2fa", VerifyLogin2FaAsync)
            .WithName("VerifyLoginTwoFactor")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");

        app.MapPost("/auth/login/resend-2fa-otp", ResendLogin2FaOtpAsync)
            .WithName("ResendLoginTwoFactorOtp")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");

        // Self-service 2FA management (authenticated)
        app.MapGet("/auth/2fa/status", GetStatusAsync)
            .WithName("GetTwoFactorStatus")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapPost("/auth/2fa/totp/setup", SetupTotpAsync)
            .WithName("SetupTwoFactorTotp")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapPost("/auth/2fa/totp/confirm", ConfirmTotpAsync)
            .WithName("ConfirmTwoFactorTotp")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapPost("/auth/2fa/email/start", StartEmailOtpEnrollmentAsync)
            .WithName("StartTwoFactorEmailEnrollment")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapPost("/auth/2fa/email/confirm", ConfirmEmailOtpEnrollmentAsync)
            .WithName("ConfirmTwoFactorEmailEnrollment")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapPost("/auth/2fa/recovery-codes/regenerate", RegenerateRecoveryCodesAsync)
            .WithName("RegenerateTwoFactorRecoveryCodes")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapPost("/auth/2fa/disable", DisableTwoFactorAsync)
            .WithName("DisableTwoFactor")
            .WithOpenApi()
            .RequireAuthorization();

        // Owner/Admin locked-out user recovery
        app.MapPost("/shop-users/{userId:int}/reset-2fa", AdminResetUserTwoFactorAsync)
            .WithName("AdminResetUserTwoFactor")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);
    }

    public static async Task<bool> IsTwoFactorRequiredForUserAsync(
        ApplicationUser user,
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var memberships = await context.ShopUsers.AsNoTracking()
            .Where(su => su.UserId == user.Id && su.Status == ShopUserStatus.Active && su.Shop.IsActive)
            .Select(su => new
            {
                su.ShopId,
                RoleNames = su.Roles.Select(r => r.Role.Name).ToList()
            })
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0) return false;
        var shopIds = memberships.Select(m => m.ShopId).ToList();
        var policies = await context.WorkspaceNotificationChannels.AsNoTracking()
            .Where(c => shopIds.Contains(c.ShopId) && c.TwoFactorEnforcement != TwoFactorEnforcementPolicy.Optional)
            .ToDictionaryAsync(c => c.ShopId, c => c.TwoFactorEnforcement, cancellationToken);

        foreach (var member in memberships)
        {
            if (!policies.TryGetValue(member.ShopId, out var policy)) continue;
            if (policy == TwoFactorEnforcementPolicy.AllStaff) return true;
            if (policy == TwoFactorEnforcementPolicy.AdminsOnly
                && member.RoleNames.Any(r => string.Equals(r, "Owner/Admin", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<IResult> VerifyLogin2FaAsync(
        [FromBody] VerifyLogin2FaRequest request,
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        JwtTokenService tokenService,
        OtpChallengeService otpService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ChallengeToken) || string.IsNullOrWhiteSpace(request.Code))
        {
            return Results.BadRequest("Challenge token and verification code are required.");
        }

        var challenge = await otpService.GetActiveChallengeAsync(request.ChallengeToken.Trim(), OtpPurpose.StaffLogin2Fa, cancellationToken);
        if (challenge is null || !challenge.UserId.HasValue)
        {
            return Results.Problem(title: "Expired challenge", detail: "Two-factor session expired. Please sign in again.", statusCode: StatusCodes.Status401Unauthorized);
        }

        if (challenge.AttemptCount >= challenge.MaxAttempts)
        {
            return Results.Problem(title: "Too many attempts", detail: "Maximum verification attempts reached. Please sign in again.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await userManager.FindByIdAsync(challenge.UserId.Value.ToString());
        if (user is null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        var code = request.Code.Trim();
        bool valid;

        if (request.UseRecoveryCode)
        {
            var redeem = await userManager.RedeemTwoFactorRecoveryCodeAsync(user, code.Replace(" ", string.Empty));
            valid = redeem.Succeeded;
        }
        else if (user.PreferredTwoFactorMethod == TwoFactorMethod.EmailOtp && challenge.Channel == NotificationChannel.Email)
        {
            valid = OtpCodeHelper.VerifyCode(code, challenge.CodeSalt, challenge.CodeHash);
        }
        else
        {
            var normalizedTotp = new string(code.Where(char.IsAsciiDigit).ToArray());
            valid = await userManager.VerifyTwoFactorTokenAsync(
                user,
                userManager.Options.Tokens.AuthenticatorTokenProvider,
                normalizedTotp);
        }

        challenge.AttemptCount += 1;
        if (!valid)
        {
            await context.SaveChangesAsync(cancellationToken);
            return Results.Problem(
                title: "Invalid code",
                detail: "The verification code is incorrect.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        challenge.ConsumedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        var token = await tokenService.CreateTokenAsync(user);
        var profile = await CurrentUser.CreateProfileAsync(user, userManager, context, cancellationToken);
        return Results.Ok(new Login.Response(token.AccessToken, token.ExpiresAt, profile));
    }

    private static async Task<IResult> ResendLogin2FaOtpAsync(
        [FromBody] ResendLogin2FaRequest request,
        UserManager<ApplicationUser> userManager,
        OtpChallengeService otpService,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var active = await otpService.GetActiveChallengeAsync(request.ChallengeToken?.Trim() ?? string.Empty, OtpPurpose.StaffLogin2Fa, cancellationToken);
        if (active is null || !active.UserId.HasValue || active.Channel != NotificationChannel.Email)
        {
            return Results.BadRequest("No active email verification session found.");
        }

        var (onCooldown, retryAfter) = await otpService.CheckCooldownAsync(OtpPurpose.StaffLogin2Fa, active.UserId, null, cancellationToken);
        if (onCooldown)
        {
            return Results.Problem(detail: $"Wait {retryAfter}s before requesting a new code.", statusCode: StatusCodes.Status429TooManyRequests);
        }

        var user = await userManager.FindByIdAsync(active.UserId.Value.ToString());
        if (user is null || string.IsNullOrWhiteSpace(user.Email)) return Results.BadRequest("User not found.");

        var (nextChallenge, plainCode) = await otpService.CreateChallengeAsync(
            active.ShopId,
            user.Id,
            null,
            OtpPurpose.StaffLogin2Fa,
            NotificationChannel.Email,
            OtpCodeHelper.MaskEmail(user.Email),
            TimeSpan.FromMinutes(5),
            cancellationToken: cancellationToken);

        var send = await dispatcher.SendDirectEmailAsync(
            active.ShopId,
            user.Email,
            "Your Servora Login Verification Code",
            $"Your 6-digit Servora login verification code is:\n\n{plainCode}\n\nThis code expires in 5 minutes.",
            null,
            "auth.login_2fa_otp",
            "User",
            user.Id,
            cancellationToken);

        if (!send.Succeeded)
        {
            return Results.Problem(detail: send.ErrorMessage ?? "Could not send verification email.", statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.Ok(new
        {
            challengeToken = nextChallenge.ChallengeToken,
            maskedEmail = nextChallenge.MaskedDestination
        });
    }

    private static async Task<IResult> GetStatusAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var user = await GetSignedInUserAsync(principal, userManager);
        if (user is null) return Results.Unauthorized();

        var recoveryCount = await userManager.CountRecoveryCodesAsync(user);
        var enforced = await IsTwoFactorRequiredForUserAsync(user, context, cancellationToken);

        return Results.Ok(new
        {
            enabled = user.TwoFactorEnabled,
            method = user.PreferredTwoFactorMethod.ToString(),
            recoveryCodesLeft = recoveryCount,
            emailOtpAvailable = dispatcher.IsSystemSmtpConfigured(),
            enforcementRequired = enforced,
            maskedEmail = OtpCodeHelper.MaskEmail(user.Email)
        });
    }

    private static async Task<IResult> SetupTotpAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var user = await GetSignedInUserAsync(principal, userManager);
        if (user is null) return Results.Unauthorized();

        var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(unformattedKey))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = user.Email ?? user.UserName ?? "user";
        var otpAuthUri = $"otpauth://totp/Servora:{Uri.EscapeDataString(email)}?secret={Uri.EscapeDataString(unformattedKey!)}&issuer=Servora&digits=6";

        return Results.Ok(new
        {
            sharedKey = FormatAuthenticatorKey(unformattedKey!),
            otpAuthUri
        });
    }

    private static async Task<IResult> ConfirmTotpAsync(
        [FromBody] ConfirmTotpRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var user = await GetSignedInUserAsync(principal, userManager);
        if (user is null) return Results.Unauthorized();

        var code = new string((request.Code ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            code);

        if (!isValid)
        {
            return Results.BadRequest("Invalid 6-digit authenticator code. Check your device time and try again.");
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        user.PreferredTwoFactorMethod = TwoFactorMethod.AuthenticatorTotp;
        await userManager.UpdateAsync(user);

        var recoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))?.ToList() ?? [];
        return Results.Ok(new
        {
            enabled = true,
            method = TwoFactorMethod.AuthenticatorTotp.ToString(),
            recoveryCodes
        });
    }

    private static async Task<IResult> StartEmailOtpEnrollmentAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        OtpChallengeService otpService,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var user = await GetSignedInUserAsync(principal, userManager);
        if (user is null || string.IsNullOrWhiteSpace(user.Email)) return Results.Unauthorized();

        if (!dispatcher.IsSystemSmtpConfigured())
        {
            return Results.BadRequest("System SMTP must be configured on the server before enabling Email OTP 2FA.");
        }

        var (challenge, plainCode) = await otpService.CreateChallengeAsync(
            null,
            user.Id,
            null,
            OtpPurpose.Staff2FaEnrollment,
            NotificationChannel.Email,
            OtpCodeHelper.MaskEmail(user.Email),
            TimeSpan.FromMinutes(10),
            cancellationToken: cancellationToken);

        var sent = await dispatcher.SendDirectEmailAsync(
            null,
            user.Email,
            "Confirm Email 2FA for Servora",
            $"Enter this 6-digit code in Servora Security settings to enable Email 2FA:\n\n{plainCode}\n\nThis code expires in 10 minutes.",
            null,
            "auth.2fa_enroll_otp",
            "User",
            user.Id,
            cancellationToken);

        if (!sent.Succeeded)
        {
            return Results.Problem(detail: sent.ErrorMessage ?? "Could not send verification email.", statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.Ok(new
        {
            challengeToken = challenge.ChallengeToken,
            maskedEmail = challenge.MaskedDestination
        });
    }

    private static async Task<IResult> ConfirmEmailOtpEnrollmentAsync(
        [FromBody] ConfirmEmailOtpRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        OtpChallengeService otpService,
        CancellationToken cancellationToken)
    {
        var user = await GetSignedInUserAsync(principal, userManager);
        if (user is null) return Results.Unauthorized();

        var verify = await otpService.VerifyChallengeAsync(
            request.ChallengeToken?.Trim() ?? string.Empty,
            request.Code?.Trim() ?? string.Empty,
            OtpPurpose.Staff2FaEnrollment,
            verifyCodeHash: true,
            cancellationToken: cancellationToken);

        if (!verify.Succeeded || verify.Challenge?.UserId != user.Id)
        {
            return Results.BadRequest(verify.ErrorMessage ?? "Invalid verification code.");
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        user.PreferredTwoFactorMethod = TwoFactorMethod.EmailOtp;
        await userManager.UpdateAsync(user);

        var recoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))?.ToList() ?? [];
        return Results.Ok(new
        {
            enabled = true,
            method = TwoFactorMethod.EmailOtp.ToString(),
            recoveryCodes
        });
    }

    private static async Task<IResult> RegenerateRecoveryCodesAsync(
        [FromBody] PasswordConfirmRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var user = await GetSignedInUserAsync(principal, userManager);
        if (user is null) return Results.Unauthorized();
        if (!user.TwoFactorEnabled) return Results.BadRequest("Two-factor authentication is not enabled.");

        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword ?? string.Empty))
        {
            return Results.BadRequest("Current password is incorrect.");
        }

        var codes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))?.ToList() ?? [];
        return Results.Ok(new { recoveryCodes = codes });
    }

    private static async Task<IResult> DisableTwoFactorAsync(
        [FromBody] PasswordConfirmRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var user = await GetSignedInUserAsync(principal, userManager);
        if (user is null) return Results.Unauthorized();

        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword ?? string.Empty))
        {
            return Results.BadRequest("Current password is incorrect.");
        }

        if (await IsTwoFactorRequiredForUserAsync(user, context, cancellationToken))
        {
            return Results.BadRequest("Your workspace security policy requires two-factor authentication for your role.");
        }

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        user.PreferredTwoFactorMethod = TwoFactorMethod.None;
        await userManager.UpdateAsync(user);

        return Results.Ok(new { enabled = false, method = TwoFactorMethod.None.ToString() });
    }

    private static async Task<IResult> AdminResetUserTwoFactorAsync(
        int userId,
        AppDbContext context,
        IShopContext shopContext,
        UserManager<ApplicationUser> userManager,
        IActivityLogger activityLogger,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var workspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);
        var belongs = await context.ShopUsers.AsNoTracking()
            .AnyAsync(su => su.ShopId == workspace.Id && su.UserId == userId, cancellationToken);
        if (!belongs) return Results.NotFound();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Results.NotFound();

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        user.PreferredTwoFactorMethod = TwoFactorMethod.None;
        await userManager.UpdateAsync(user);

        await activityLogger.LogAsync(
            "User",
            userId,
            "UserTwoFactorReset",
            $"Two-factor authentication reset by administrator for {user.Email}",
            cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            await dispatcher.SendDirectEmailAsync(
                workspace.Id,
                user.Email,
                $"[{workspace.Name}] Your Two-Factor Authentication Was Reset",
                $"Hello {user.FirstName},\n\nAn administrator in {workspace.Name} reset two-factor authentication on your Servora account. Please sign in and re-enroll in 2FA from Settings -> Security.",
                null,
                "auth.admin_2fa_reset",
                "User",
                user.Id,
                cancellationToken);
        }

        return Results.Ok(new { message = "Two-factor authentication has been reset for this user." });
    }

    private static async Task<ApplicationUser?> GetSignedInUserAsync(ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var rawId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out var userId)) return null;
        return await userManager.FindByIdAsync(userId.ToString());
    }

    private static string FormatAuthenticatorKey(string unformattedKey)
    {
        var sb = new StringBuilder();
        var pos = 0;
        while (pos + 4 < unformattedKey.Length)
        {
            sb.Append(unformattedKey.AsSpan(pos, 4)).Append(' ');
            pos += 4;
        }
        if (pos < unformattedKey.Length)
        {
            sb.Append(unformattedKey.AsSpan(pos));
        }
        return sb.ToString().ToLowerInvariant();
    }
}
