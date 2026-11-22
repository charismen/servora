using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Notifications;
using Servora.Api.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Servora.Api.Features.Identity.Auth;

public static class Login
{
    public sealed record Request(string Email, string Password);

    public sealed record Response(
        string AccessToken,
        DateTimeOffset ExpiresAt,
        CurrentUser.UserProfile User);

    public sealed record TwoFactorChallengeResponse(
        bool RequiresTwoFactor,
        string ChallengeToken,
        string Method,
        string? MaskedEmail);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Email).NotEmpty().EmailAddress();
            RuleFor(request => request.Password).NotEmpty();
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async (
            [FromBody] Request request,
            IValidator<Request> validator,
            UserManager<ApplicationUser> userManager,
            AppDbContext context,
            JwtTokenService tokenService,
            OtpChallengeService otpService,
            NotificationDispatcherService dispatcher,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                return Results.Problem(
                    title: "Invalid credentials",
                    detail: "The supplied email and password do not match an active user.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (user.TwoFactorEnabled)
            {
                var method = user.PreferredTwoFactorMethod == TwoFactorMethod.None
                    ? TwoFactorMethod.AuthenticatorTotp
                    : user.PreferredTwoFactorMethod;
                var maskedEmail = !string.IsNullOrWhiteSpace(user.Email)
                    ? OtpCodeHelper.MaskEmail(user.Email)
                    : null;

                var (challenge, plainCode) = await otpService.CreateChallengeAsync(
                    shopId: null,
                    userId: user.Id,
                    serviceTicketId: null,
                    purpose: OtpPurpose.StaffLogin2Fa,
                    channel: NotificationChannel.Email,
                    maskedDestination: maskedEmail ?? "authenticator",
                    ttl: TimeSpan.FromMinutes(5),
                    maxAttempts: 5,
                    cancellationToken: cancellationToken);

                if (method == TwoFactorMethod.EmailOtp && !string.IsNullOrWhiteSpace(user.Email))
                {
                    var subject = "Servora sign-in verification code";
                    var text = $"Your Servora sign-in verification code is {plainCode}. It expires in 5 minutes.";
                    var html = MailKitEmailSender.BuildDefaultHtmlEmail(
                        "Servora",
                        "Sign-in verification code",
                        $"Use this 6-digit verification code to finish signing in:\n\n{plainCode}\n\nThis code expires in 5 minutes. If you did not try to sign in, change your password right away.");
                    await dispatcher.SendDirectEmailAsync(
                        shopId: null,
                        toEmail: user.Email,
                        subject: subject,
                        plainTextBody: text,
                        htmlBody: html,
                        eventType: "auth.2fa_otp",
                        referenceEntityType: "User",
                        referenceEntityId: user.Id,
                        cancellationToken: cancellationToken);
                }

                return Results.Ok(new TwoFactorChallengeResponse(
                    RequiresTwoFactor: true,
                    ChallengeToken: challenge.ChallengeToken,
                    Method: method.ToString(),
                    MaskedEmail: maskedEmail));
            }

            var token = await tokenService.CreateTokenAsync(user);
            var profile = await CurrentUser.CreateProfileAsync(user, userManager, context, cancellationToken);

            return Results.Ok(new Response(token.AccessToken, token.ExpiresAt, profile));
        })
        .WithName("Login")
        .WithOpenApi()
        .AllowAnonymous();
    }
}
