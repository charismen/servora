using Servora.Api.Features.Identity.Users;
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

            var token = await tokenService.CreateTokenAsync(user);
            var profile = await CurrentUser.CreateProfileAsync(user, userManager, context, cancellationToken);

            return Results.Ok(new Response(token.AccessToken, token.ExpiresAt, profile));
        })
        .WithName("Login")
        .WithOpenApi()
        .AllowAnonymous();
    }
}
