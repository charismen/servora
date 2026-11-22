using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.Security;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Payments;

public static class PaymentGatewayEndpoints
{
    public sealed record PaymentGatewaySettingsDto(
        string Provider,
        string Environment,
        bool IsEnabled,
        bool HasApiKey,
        string? ApiKeyLast4,
        bool HasWebhookSecret,
        string? WebhookSecretLast4,
        string WebhookUrl,
        int DefaultLinkExpiryHours,
        bool EnablePublicTrackingPayment,
        bool RequirePhoneVerificationForPublicPayment,
        bool WorkspaceEligible,
        string? WorkspaceEligibilityReason,
        bool EncryptionKeyConfigured,
        bool CanStoreSecrets);

    public sealed record UpdatePaymentGatewaySettingsRequest(
        string Provider,
        string Environment,
        bool IsEnabled,
        string? ApiKey,
        bool ClearApiKey,
        string? WebhookSecret,
        bool ClearWebhookSecret,
        int DefaultLinkExpiryHours,
        bool EnablePublicTrackingPayment,
        bool RequirePhoneVerificationForPublicPayment);

    public sealed record CreatePaymentLinkRequest(
        decimal? Amount,
        int? ExpiresInHours,
        string? Description);

    public sealed record TicketPaymentLinksResponse(
        bool GatewayEnabled,
        string? Provider,
        string? Environment,
        bool WorkspaceEligible,
        string? WorkspaceEligibilityReason,
        int DefaultLinkExpiryHours,
        IReadOnlyList<PaymentLinkDto> Links);

    public sealed record PublicTrackingPaymentRequest(string? PhoneLastFour);

    public sealed record PublicTrackingPaymentSummaryDto(
        bool OnlinePaymentEnabled,
        bool RequiresPhoneVerification,
        string PaymentStatus,
        decimal? TotalAmount,
        decimal AmountPaid,
        decimal RemainingBalance,
        string CurrencyCode,
        string? ActivePaymentUrl,
        DateTimeOffset? ActivePaymentExpiresAt);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Workspace Payment Gateway Settings
        app.MapGet("/settings/payment-gateway", GetSettingsAsync)
            .WithName("GetPaymentGatewaySettings")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPut("/settings/payment-gateway", UpdateSettingsAsync)
            .WithName("UpdatePaymentGatewaySettings")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/settings/payment-gateway/rotate-webhook-token", RotateWebhookTokenAsync)
            .WithName("RotatePaymentGatewayWebhookToken")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/settings/payment-gateway/test", TestConnectionAsync)
            .WithName("TestPaymentGatewayConnection")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        // Ticket Payment Links
        app.MapGet("/service-tickets/{id:int}/payment-links", ListTicketPaymentLinksAsync)
            .WithName("ListTicketPaymentLinks")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPost("/service-tickets/{id:int}/payment-links", CreateTicketPaymentLinkAsync)
            .WithName("CreateTicketPaymentLink")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/service-tickets/{id:int}/payment-links/{intentId:int}/sync", SyncTicketPaymentLinkAsync)
            .WithName("SyncTicketPaymentLink")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/service-tickets/{id:int}/payment-links/{intentId:int}/cancel", CancelTicketPaymentLinkAsync)
            .WithName("CancelTicketPaymentLink")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        // Public Tracking Payment Endpoints (Unauthenticated, token-gated, rate-limited)
        app.MapPost("/track/{token}/payment-summary", GetPublicTrackingPaymentSummaryAsync)
            .WithName("GetPublicTrackingPaymentSummary")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");

        app.MapPost("/track/{token}/checkout", CreatePublicTrackingCheckoutAsync)
            .WithName("CreatePublicTrackingCheckout")
            .WithOpenApi()
            .AllowAnonymous()
            .RequireRateLimiting("PublicTrackingVerify");

        // Unauthenticated Provider Webhook Receiver (Signature-verified per workspace)
        app.MapPost("/webhooks/payments/{provider}/{webhookToken}", ReceiveWebhookAsync)
            .WithName("ReceivePaymentGatewayWebhook")
            .WithOpenApi()
            .AllowAnonymous();
    }

    private static async Task<IResult> GetSettingsAsync(
        HttpContext httpContext,
        AppDbContext context,
        IShopContext shopContext,
        PaymentCheckoutService checkoutService,
        ISecretProtector secretProtector,
        CancellationToken cancellationToken)
    {
        var workspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);
        var config = await checkoutService.GetOrCreateGatewayConfigAsync(workspace.Id, cancellationToken);
        return Results.Ok(ToSettingsDto(httpContext, workspace, config, secretProtector));
    }

    private static async Task<IResult> UpdateSettingsAsync(
        [FromBody] UpdatePaymentGatewaySettingsRequest request,
        HttpContext httpContext,
        AppDbContext context,
        IShopContext shopContext,
        PaymentCheckoutService checkoutService,
        ISecretProtector secretProtector,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentGatewayProvider>(request.Provider, true, out var provider) || !Enum.IsDefined(provider))
        {
            return Results.BadRequest("Invalid payment gateway provider.");
        }

        if (!Enum.TryParse<PaymentGatewayEnvironment>(request.Environment, true, out var environment) || !Enum.IsDefined(environment))
        {
            return Results.BadRequest("Invalid payment gateway environment.");
        }

        if (request.DefaultLinkExpiryHours < 1 || request.DefaultLinkExpiryHours > 168)
        {
            return Results.BadRequest("Default link expiry hours must be between 1 and 168.");
        }

        var workspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);
        var (eligible, eligibilityReason) = PaymentCheckoutService.EvaluateWorkspaceEligibility(workspace);
        var config = await checkoutService.GetOrCreateGatewayConfigAsync(workspace.Id, cancellationToken);

        var wantsToStoreSecret = !string.IsNullOrWhiteSpace(request.ApiKey) || !string.IsNullOrWhiteSpace(request.WebhookSecret);
        if (wantsToStoreSecret && !secretProtector.CanStoreSecrets)
        {
            return Results.BadRequest(
                "Security:SecretEncryptionKey must be configured on the server before storing payment gateway credentials in Production.");
        }

        var effectiveEnabled = request.IsEnabled && provider != PaymentGatewayProvider.None;
        if (effectiveEnabled && !eligible)
        {
            return Results.BadRequest(eligibilityReason ?? "Online payments are not eligible for this workspace.");
        }

        config.Provider = provider;
        config.Environment = environment;
        config.DefaultLinkExpiryHours = request.DefaultLinkExpiryHours;

        if (request.ClearApiKey)
        {
            config.ApiKeyEncrypted = null;
            config.ApiKeyLast4 = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            var trimmedKey = request.ApiKey.Trim();
            config.ApiKeyEncrypted = secretProtector.Protect(trimmedKey);
            config.ApiKeyLast4 = PaymentCheckoutService.ExtractLast4(trimmedKey);
        }

        if (request.ClearWebhookSecret)
        {
            config.WebhookSecretEncrypted = null;
            config.WebhookSecretLast4 = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.WebhookSecret))
        {
            var trimmedSecret = request.WebhookSecret.Trim();
            config.WebhookSecretEncrypted = secretProtector.Protect(trimmedSecret);
            config.WebhookSecretLast4 = PaymentCheckoutService.ExtractLast4(trimmedSecret);
        }

        if (effectiveEnabled && string.IsNullOrWhiteSpace(config.ApiKeyEncrypted))
        {
            return Results.BadRequest("An API key is required before enabling online payments.");
        }

        config.IsEnabled = effectiveEnabled;
        config.EnablePublicTrackingPayment = effectiveEnabled && request.EnablePublicTrackingPayment;
        config.RequirePhoneVerificationForPublicPayment = request.RequirePhoneVerificationForPublicPayment;

        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync(
            "Settings",
            workspace.Id,
            "PaymentGatewayUpdated",
            $"Payment gateway settings updated ({config.Provider}, {(config.IsEnabled ? "Enabled" : "Disabled")})",
            metadataJson: JsonSerializer.Serialize(new
            {
                provider = config.Provider.ToString(),
                environment = config.Environment.ToString(),
                isEnabled = config.IsEnabled,
                enablePublicTrackingPayment = config.EnablePublicTrackingPayment
            }),
            cancellationToken: cancellationToken);

        return Results.Ok(ToSettingsDto(httpContext, workspace, config, secretProtector));
    }

    private static async Task<IResult> RotateWebhookTokenAsync(
        HttpContext httpContext,
        AppDbContext context,
        IShopContext shopContext,
        PaymentCheckoutService checkoutService,
        ISecretProtector secretProtector,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var workspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);
        var config = await checkoutService.GetOrCreateGatewayConfigAsync(workspace.Id, cancellationToken);
        config.WebhookToken = PaymentCheckoutService.GenerateWebhookToken();

        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync(
            "Settings",
            workspace.Id,
            "PaymentGatewayWebhookRotated",
            "Payment gateway webhook URL token rotated",
            cancellationToken: cancellationToken);

        return Results.Ok(ToSettingsDto(httpContext, workspace, config, secretProtector));
    }

    private static async Task<IResult> TestConnectionAsync(
        IShopContext shopContext,
        PaymentCheckoutService checkoutService,
        IPaymentGatewayRegistry gatewayRegistry,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var config = await checkoutService.GetOrCreateGatewayConfigAsync(shopId, cancellationToken);

        if (config.Provider == PaymentGatewayProvider.None)
        {
            return Results.BadRequest("Select a payment gateway provider first.");
        }

        var gateway = gatewayRegistry.Resolve(config.Provider);
        if (gateway is null)
        {
            return Results.BadRequest($"Provider '{config.Provider}' is not available.");
        }

        GatewayCredentials? creds;
        try
        {
            creds = checkoutService.TryDecryptCredentials(config);
        }
        catch (Exception)
        {
            return Results.BadRequest("Saved credentials could not be decrypted. Please re-enter and save your API key.");
        }

        if (creds is null || string.IsNullOrWhiteSpace(creds.ApiKey))
        {
            return Results.BadRequest("Save an API key before testing the connection.");
        }

        var result = await gateway.TestConnectionAsync(creds, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListTicketPaymentLinksAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        PaymentCheckoutService checkoutService,
        CancellationToken cancellationToken)
    {
        var workspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);
        var ticketExists = await context.ServiceTickets.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.ShopId == workspace.Id && x.DeletedAt == null, cancellationToken);
        if (!ticketExists) return Results.NotFound();

        var (eligible, reason) = PaymentCheckoutService.EvaluateWorkspaceEligibility(workspace);
        var config = await context.WorkspacePaymentGateways.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShopId == workspace.Id, cancellationToken);
        var links = await checkoutService.ListTicketLinksAsync(workspace.Id, id, cancellationToken);

        var gatewayEnabled = eligible && config is not null && config.IsEnabled && config.Provider != PaymentGatewayProvider.None;
        return Results.Ok(new TicketPaymentLinksResponse(
            GatewayEnabled: gatewayEnabled,
            Provider: config?.Provider.ToString(),
            Environment: config?.Environment.ToString(),
            WorkspaceEligible: eligible,
            WorkspaceEligibilityReason: reason,
            DefaultLinkExpiryHours: config?.DefaultLinkExpiryHours ?? 24,
            Links: links));
    }

    private static async Task<IResult> CreateTicketPaymentLinkAsync(
        int id,
        [FromBody] CreatePaymentLinkRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IConfiguration configuration,
        IShopContext shopContext,
        PaymentCheckoutService checkoutService,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var actorUserId = TryGetUserId(principal);
        var publicFrontendBaseUrl = ResolvePublicFrontendBaseUrl(configuration, httpContext);

        var result = await checkoutService.CreatePaymentLinkAsync(
            shopId,
            id,
            new CreatePaymentLinkCommand(
                request.Amount,
                request.ExpiresInHours,
                request.Description,
                publicFrontendBaseUrl,
                actorUserId),
            cancellationToken);

        return result.Succeeded
            ? Results.Created($"/api/v1/service-tickets/{id}/payment-links/{result.Value!.Id}", result.Value)
            : Results.Problem(detail: result.ErrorMessage, statusCode: result.StatusCode);
    }

    private static async Task<IResult> SyncTicketPaymentLinkAsync(
        int id,
        int intentId,
        ClaimsPrincipal principal,
        IShopContext shopContext,
        PaymentCheckoutService checkoutService,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var result = await checkoutService.SyncIntentStatusAsync(shopId, id, intentId, TryGetUserId(principal), cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : Results.Problem(detail: result.ErrorMessage, statusCode: result.StatusCode);
    }

    private static async Task<IResult> CancelTicketPaymentLinkAsync(
        int id,
        int intentId,
        ClaimsPrincipal principal,
        IShopContext shopContext,
        PaymentCheckoutService checkoutService,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var result = await checkoutService.CancelIntentAsync(shopId, id, intentId, TryGetUserId(principal), cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : Results.Problem(detail: result.ErrorMessage, statusCode: result.StatusCode);
    }

    private static async Task<IResult> GetPublicTrackingPaymentSummaryAsync(
        string token,
        [FromBody] PublicTrackingPaymentRequest? request,
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolvePublicPaymentTicketAsync(token, request?.PhoneLastFour, context, cancellationToken);
        if (!resolved.Succeeded)
        {
            return Results.Problem(detail: resolved.ErrorMessage, statusCode: resolved.StatusCode);
        }

        var (ticket, gatewayConfig, billing, activePending) = resolved.Value!;
        return Results.Ok(new PublicTrackingPaymentSummaryDto(
            OnlinePaymentEnabled: true,
            RequiresPhoneVerification: gatewayConfig.RequirePhoneVerificationForPublicPayment,
            PaymentStatus: billing.PaymentStatus.ToString(),
            TotalAmount: billing.TotalAmount,
            AmountPaid: billing.AmountPaid,
            RemainingBalance: billing.RemainingBalance,
            CurrencyCode: ticket.Shop.CurrencyCode,
            ActivePaymentUrl: activePending?.PaymentUrl,
            ActivePaymentExpiresAt: activePending?.ExpiresAt));
    }

    private static async Task<IResult> CreatePublicTrackingCheckoutAsync(
        string token,
        [FromBody] PublicTrackingPaymentRequest? request,
        HttpContext httpContext,
        IConfiguration configuration,
        AppDbContext context,
        PaymentCheckoutService checkoutService,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolvePublicPaymentTicketAsync(token, request?.PhoneLastFour, context, cancellationToken);
        if (!resolved.Succeeded)
        {
            return Results.Problem(detail: resolved.ErrorMessage, statusCode: resolved.StatusCode);
        }

        var (ticket, _, billing, activePending) = resolved.Value!;
        if (billing.RemainingBalance <= 0m)
        {
            return Results.BadRequest("There is no remaining balance due on this ticket.");
        }

        // Reuse active pending checkout link if it matches the full remaining balance and has not expired.
        if (activePending is not null
            && activePending.Amount == billing.RemainingBalance
            && activePending.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2)
            && !string.IsNullOrWhiteSpace(activePending.PaymentUrl))
        {
            return Results.Ok(new
            {
                paymentUrl = activePending.PaymentUrl,
                expiresAt = activePending.ExpiresAt,
                amount = activePending.Amount,
                currencyCode = activePending.CurrencyCode
            });
        }

        var publicFrontendBaseUrl = ResolvePublicFrontendBaseUrl(configuration, httpContext);
        var created = await checkoutService.CreatePaymentLinkAsync(
            ticket.ShopId,
            ticket.Id,
            new CreatePaymentLinkCommand(
                Amount: billing.RemainingBalance,
                ExpiresInHours: null,
                Description: null,
                PublicFrontendBaseUrl: publicFrontendBaseUrl,
                ActorUserId: null),
            cancellationToken);

        if (!created.Succeeded)
        {
            return Results.Problem(detail: created.ErrorMessage, statusCode: created.StatusCode);
        }

        return Results.Ok(new
        {
            paymentUrl = created.Value!.PaymentUrl,
            expiresAt = created.Value.ExpiresAt,
            amount = created.Value.Amount,
            currencyCode = created.Value.CurrencyCode
        });
    }

    private static async Task<IResult> ReceiveWebhookAsync(
        string provider,
        string webhookToken,
        HttpContext httpContext,
        AppDbContext context,
        IPaymentGatewayRegistry gatewayRegistry,
        PaymentCheckoutService checkoutService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("PaymentGatewayWebhook");
        if (!Enum.TryParse<PaymentGatewayProvider>(provider, true, out var parsedProvider)
            || !Enum.IsDefined(parsedProvider)
            || parsedProvider == PaymentGatewayProvider.None)
        {
            return Results.NotFound();
        }

        var gatewayConfig = await context.WorkspacePaymentGateways.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WebhookToken == webhookToken && x.Provider == parsedProvider, cancellationToken);
        if (gatewayConfig is null || !gatewayConfig.IsEnabled)
        {
            return Results.NotFound();
        }

        var gateway = gatewayRegistry.Resolve(parsedProvider);
        if (gateway is null)
        {
            return Results.NotFound();
        }

        GatewayCredentials? creds;
        try
        {
            creds = checkoutService.TryDecryptCredentials(gatewayConfig);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt credentials for webhook workspace {ShopId}.", gatewayConfig.ShopId);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        if (creds is null)
        {
            return Results.Unauthorized();
        }

        string rawBody;
        using (var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }

        var verification = await gateway.VerifyAndParseWebhookAsync(creds, httpContext.Request, rawBody, cancellationToken);
        if (!verification.IsValid)
        {
            logger.LogWarning(
                "Rejected {Provider} webhook for workspace {ShopId}: {Error}",
                parsedProvider,
                gatewayConfig.ShopId,
                verification.ErrorMessage);
            return Results.Unauthorized();
        }

        var eventId = string.IsNullOrWhiteSpace(verification.ProviderEventId)
            ? $"evt-{Guid.NewGuid():N}"
            : verification.ProviderEventId.Trim();
        var eventType = string.IsNullOrWhiteSpace(verification.EventType) ? "unknown" : verification.EventType.Trim();

        // Deduplicate webhook delivery per workspace + provider + eventId
        var webhookEvent = new PaymentWebhookEvent
        {
            ShopId = gatewayConfig.ShopId,
            Provider = parsedProvider,
            ProviderEventId = eventId.Length > 160 ? eventId[..160] : eventId,
            EventType = eventType.Length > 80 ? eventType[..80] : eventType,
            ProcessingStatus = "Received",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        context.PaymentWebhookEvents.Add(webhookEvent);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.Entry(webhookEvent).State = EntityState.Detached;
            return Results.Ok(new { received = true, duplicate = true });
        }

        try
        {
            if (verification.MappedStatus is null)
            {
                webhookEvent.ProcessingStatus = "Ignored";
                webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                return Results.Ok(new { received = true, ignored = true });
            }

            PaymentCheckoutIntent? intent = null;
            if (!string.IsNullOrWhiteSpace(verification.ProviderCheckoutId))
            {
                intent = await context.PaymentCheckoutIntents.AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.ShopId == gatewayConfig.ShopId && x.ProviderCheckoutId == verification.ProviderCheckoutId,
                        cancellationToken);
            }

            if (intent is null && !string.IsNullOrWhiteSpace(verification.ExternalReference))
            {
                intent = await context.PaymentCheckoutIntents.AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.ShopId == gatewayConfig.ShopId && x.ExternalReference == verification.ExternalReference,
                        cancellationToken);
            }

            if (intent is null)
            {
                webhookEvent.ProcessingStatus = "Unmatched";
                webhookEvent.ErrorMessage = "No matching payment checkout intent found for this workspace.";
                webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                return Results.Ok(new { received = true, matched = false });
            }

            await checkoutService.ApplyProviderStatusAsync(
                gatewayConfig.ShopId,
                intent.Id,
                verification.MappedStatus.Value,
                verification.ProviderPaymentMethod,
                verification.PaidAt,
                verification.FailureReason,
                actorUserId: null,
                source: "webhook",
                cancellationToken);

            webhookEvent.ProcessingStatus = "Processed";
            webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { received = true, processed = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing {Provider} webhook event {EventId}.", parsedProvider, eventId);
            webhookEvent.ProcessingStatus = "Failed";
            webhookEvent.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private sealed record PublicPaymentContext(
        ServiceTicket Ticket,
        WorkspacePaymentGateway GatewayConfig,
        TicketBillingSnapshot Billing,
        PaymentCheckoutIntent? ActivePending);

    private static async Task<PaymentOperationResult<PublicPaymentContext>> ResolvePublicPaymentTicketAsync(
        string token,
        string? phoneLastFour,
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var ticket = await context.ServiceTickets
            .AsNoTracking()
            .Include(x => x.Shop)
            .Include(x => x.Status)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(
                x => x.PublicTrackingEnabled && x.PublicTrackingToken == token && x.DeletedAt == null,
                cancellationToken);

        if (ticket is null)
        {
            return PaymentOperationResult<PublicPaymentContext>.Fail("Tracking ticket not found.", StatusCodes.Status404NotFound);
        }

        var (eligible, reason) = PaymentCheckoutService.EvaluateWorkspaceEligibility(ticket.Shop);
        if (!eligible || ticket.IsInternalWorkOrder || ticket.Status.IsCancelled || ticket.PaymentStatus == PaymentStatus.Waived)
        {
            return PaymentOperationResult<PublicPaymentContext>.Fail(reason ?? "Online payment is not available for this ticket.", StatusCodes.Status404NotFound);
        }

        var gatewayConfig = await context.WorkspacePaymentGateways.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShopId == ticket.ShopId, cancellationToken);
        if (gatewayConfig is null
            || !gatewayConfig.IsEnabled
            || gatewayConfig.Provider == PaymentGatewayProvider.None
            || !gatewayConfig.EnablePublicTrackingPayment)
        {
            return PaymentOperationResult<PublicPaymentContext>.Fail("Online payment on public tracking is not enabled.", StatusCodes.Status404NotFound);
        }

        if (gatewayConfig.RequirePhoneVerificationForPublicPayment)
        {
            var inputDigits = new string((phoneLastFour ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
            if (inputDigits.Length != 4)
            {
                return PaymentOperationResult<PublicPaymentContext>.Fail("Enter the last 4 digits of the registered phone number.");
            }

            var phoneRaw = !string.IsNullOrWhiteSpace(ticket.CustomerPhoneSnapshot)
                ? ticket.CustomerPhoneSnapshot
                : ticket.Customer.PhoneNumber;
            var phoneDigits = new string((phoneRaw ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
            if (phoneDigits.Length < 4)
            {
                return PaymentOperationResult<PublicPaymentContext>.Fail("Phone number verification failed.");
            }

            var expectedBytes = Encoding.UTF8.GetBytes(phoneDigits[^4..]);
            var actualBytes = Encoding.UTF8.GetBytes(inputDigits);
            if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
            {
                return PaymentOperationResult<PublicPaymentContext>.Fail("Phone number digits did not match.");
            }
        }

        var billing = await TicketBillingService.GetBillingSnapshotAsync(context, ticket.ShopId, ticket, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var activePending = await context.PaymentCheckoutIntents.AsNoTracking()
            .Where(x => x.ShopId == ticket.ShopId
                && x.ServiceTicketId == ticket.Id
                && x.Status == PaymentCheckoutStatus.Pending
                && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return PaymentOperationResult<PublicPaymentContext>.Ok(new PublicPaymentContext(ticket, gatewayConfig, billing, activePending));
    }

    private static PaymentGatewaySettingsDto ToSettingsDto(
        HttpContext httpContext,
        Shops.Shop workspace,
        WorkspacePaymentGateway config,
        ISecretProtector secretProtector)
    {
        var (eligible, reason) = PaymentCheckoutService.EvaluateWorkspaceEligibility(workspace);
        var providerSegment = config.Provider == PaymentGatewayProvider.None
            ? "{provider}"
            : config.Provider.ToString().ToLowerInvariant();
        var apiBase = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var webhookUrl = $"{apiBase}/api/v1/webhooks/payments/{providerSegment}/{config.WebhookToken}";

        return new PaymentGatewaySettingsDto(
            Provider: config.Provider.ToString(),
            Environment: config.Environment.ToString(),
            IsEnabled: config.IsEnabled,
            HasApiKey: !string.IsNullOrWhiteSpace(config.ApiKeyEncrypted),
            ApiKeyLast4: config.ApiKeyLast4,
            HasWebhookSecret: !string.IsNullOrWhiteSpace(config.WebhookSecretEncrypted),
            WebhookSecretLast4: config.WebhookSecretLast4,
            WebhookUrl: webhookUrl,
            DefaultLinkExpiryHours: config.DefaultLinkExpiryHours,
            EnablePublicTrackingPayment: config.EnablePublicTrackingPayment,
            RequirePhoneVerificationForPublicPayment: config.RequirePhoneVerificationForPublicPayment,
            WorkspaceEligible: eligible,
            WorkspaceEligibilityReason: reason,
            EncryptionKeyConfigured: secretProtector.IsEncryptionKeyConfigured,
            CanStoreSecrets: secretProtector.CanStoreSecrets);
    }

    private static string ResolvePublicFrontendBaseUrl(IConfiguration configuration, HttpContext httpContext)
    {
        var configured = configuration["PublicFrontendBaseUrl"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}".TrimEnd('/');
    }

    private static int? TryGetUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
