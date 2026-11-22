using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Blueprints;
using Servora.Api.Features.Identity.Roles;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.Organizations;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Identity.Auth;

public static class AccountAuthEndpoints
{
    public sealed record ForgotPasswordRequest(string Email);
    public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
    public sealed record RegisterRequest(
        string FirstName,
        string LastName,
        string Email,
        string Password,
        string OrganizationName,
        string? WorkspaceName = null,
        string? BlueprintKey = null);
    public sealed record VerifyEmailRequest(string Email, string Token);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/config", GetAuthConfigAsync)
            .WithName("GetAuthConfig")
            .WithOpenApi()
            .AllowAnonymous();

        app.MapPost("/auth/forgot-password", ForgotPasswordAsync)
            .WithName("ForgotPassword")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");

        app.MapPost("/auth/reset-password", ResetPasswordAsync)
            .WithName("ResetPassword")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");

        app.MapPost("/auth/register", RegisterAsync)
            .WithName("RegisterAccount")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");

        app.MapPost("/auth/verify-email", VerifyEmailAsync)
            .WithName("VerifyAccountEmail")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");
    }

    private static IResult GetAuthConfigAsync(
        IConfiguration configuration,
        NotificationDispatcherService dispatcher)
    {
        return Results.Ok(new
        {
            allowPublicRegistration = configuration.GetValue("Auth:AllowPublicRegistration", false),
            emailAuthEnabled = dispatcher.IsSystemSmtpConfigured()
        });
    }

    private static async Task<IResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return Results.BadRequest("Enter a valid email address.");
        }

        // Anti-enumeration: always return 200 OK whether or not the account exists
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && user.IsActive)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var baseUrl = (configuration["PublicFrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
            var resetUrl = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(encodedToken)}";

            var plain = $"Hello {user.FirstName},\n\nWe received a request to reset your Servora password.\n\nUse the link below to choose a new password:\n{resetUrl}\n\nIf you did not request this, you can safely ignore this email.";
            var html = MailKitEmailSender.BuildDefaultHtmlEmail("Servora", "Reset Your Password", plain, resetUrl, "Reset Password");

            await dispatcher.SendDirectEmailAsync(
                shopId: null,
                toEmail: user.Email!,
                subject: "Reset your Servora password",
                plainTextBody: plain,
                htmlBody: html,
                eventType: "auth.forgot_password",
                referenceEntityType: "User",
                referenceEntityId: user.Id,
                cancellationToken: cancellationToken);
        }

        return Results.Ok(new
        {
            message = "If an active account matches that email, password reset instructions have been sent."
        });
    }

    private static async Task<IResult> ResetPasswordAsync(
        [FromBody] ResetPasswordRequest request,
        UserManager<ApplicationUser> userManager)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.BadRequest("Email, reset token, and new password are required.");
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            return Results.BadRequest("Invalid or expired password reset link.");
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token.Trim()));
        }
        catch
        {
            decodedToken = request.Token.Trim();
        }

        var result = await userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
        if (!result.Succeeded)
        {
            var err = string.Join(" ", result.Errors.Select(e => e.Description));
            return Results.BadRequest(string.IsNullOrWhiteSpace(err) ? "Password reset failed." : err);
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        return Results.Ok(new { message = "Your password has been updated. You can now sign in." });
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        BlueprintProvisioningService provisioningService,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Auth:AllowPublicRegistration", false))
        {
            return Results.Problem(
                title: "Registration disabled",
                detail: "Self-service registration is disabled on this Servora instance. Ask an administrator for an invitation.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var email = request.Email?.Trim() ?? string.Empty;
        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var lastName = request.LastName?.Trim() ?? string.Empty;
        var orgName = request.OrganizationName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(orgName))
        {
            return Results.BadRequest("First name, email, password, and organization name are required.");
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return Results.Conflict("An account with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            EmailConfirmed = !dispatcher.IsSystemSmtpConfigured(),
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Results.BadRequest(string.Join(" ", createResult.Errors.Select(e => e.Description)));
        }

        var slugBase = new string(orgName.ToLowerInvariant().Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray());
        if (string.IsNullOrWhiteSpace(slugBase)) slugBase = "org";
        var suffix = Guid.NewGuid().ToString("N")[..6];

        var org = new Organization
        {
            Name = orgName,
            Slug = $"{slugBase}-{suffix}",
            IsActive = true
        };
        context.Organizations.Add(org);
        await context.SaveChangesAsync(cancellationToken);

        context.OrganizationUsers.Add(new OrganizationUser
        {
            OrganizationId = org.Id,
            UserId = user.Id,
            Role = OrganizationRole.Owner,
            Status = OrganizationUserStatus.Active
        });

        var workspaceName = string.IsNullOrWhiteSpace(request.WorkspaceName) ? orgName : request.WorkspaceName.Trim();
        var blueprintKey = string.IsNullOrWhiteSpace(request.BlueprintKey) ? "it_computer" : request.BlueprintKey.Trim();
        var workspace = new Shop
        {
            OrganizationId = org.Id,
            Name = workspaceName,
            Slug = $"{slugBase}-ws-{suffix}",
            OperatingMode = OperatingMode.Commercial,
            IndustryBlueprintKey = blueprintKey,
            DefaultLocale = "en",
            TimeZone = "Asia/Jakarta",
            CurrencyCode = "IDR",
            IsActive = true
        };
        context.Shops.Add(workspace);
        await context.SaveChangesAsync(cancellationToken);

        await provisioningService.ApplyBlueprintAsync(workspace, blueprintKey, cancellationToken: cancellationToken);

        var ownerRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Owner/Admin", cancellationToken);
        var membership = new ShopUser
        {
            ShopId = workspace.Id,
            UserId = user.Id,
            Status = ShopUserStatus.Active
        };
        context.ShopUsers.Add(membership);
        await context.SaveChangesAsync(cancellationToken);

        if (ownerRole is not null)
        {
            context.ShopUserRoles.Add(new ShopUserRole { ShopUserId = membership.Id, RoleId = ownerRole.Id });
            await context.SaveChangesAsync(cancellationToken);
        }

        var verificationSent = false;
        if (dispatcher.IsSystemSmtpConfigured())
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var baseUrl = (configuration["PublicFrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
            var verifyUrl = $"{baseUrl}/verify-email?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(encodedToken)}";

            var plain = $"Welcome to Servora, {user.FirstName}!\n\nYour organization '{org.Name}' is ready. Please verify your email address:\n{verifyUrl}";
            var html = MailKitEmailSender.BuildDefaultHtmlEmail("Servora", "Verify Your Email", plain, verifyUrl, "Verify Email");
            var res = await dispatcher.SendDirectEmailAsync(
                workspace.Id,
                user.Email!,
                "Welcome to Servora — Verify your email",
                plain,
                html,
                "auth.register_verify",
                "User",
                user.Id,
                cancellationToken);
            verificationSent = res.Succeeded;
        }

        return Results.Created($"/api/v1/workspaces/{workspace.Id}", new
        {
            userId = user.Id,
            organizationId = org.Id,
            workspaceId = workspace.Id,
            verificationEmailSent = verificationSent,
            message = verificationSent
                ? "Account created! Please check your email to verify your address before signing in."
                : "Account created! You can now sign in."
        });
    }

    private static async Task<IResult> VerifyEmailAsync(
        [FromBody] VerifyEmailRequest request,
        UserManager<ApplicationUser> userManager)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Token))
        {
            return Results.BadRequest("Email and verification token are required.");
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null) return Results.BadRequest("Invalid verification link.");

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token.Trim()));
        }
        catch
        {
            decodedToken = request.Token.Trim();
        }

        var result = await userManager.ConfirmEmailAsync(user, decodedToken);
        if (!result.Succeeded)
        {
            return Results.BadRequest("Email verification failed or token has expired.");
        }

        return Results.Ok(new { message = "Email verified! You can now sign in." });
    }
}
