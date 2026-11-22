using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Activity;
using Servora.Api.Features.Blueprints;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.Security;

namespace Servora.Api.Features.Payments;

public sealed record PaymentLinkDto(
    int Id,
    int ServiceTicketId,
    string Provider,
    string Environment,
    string Status,
    decimal Amount,
    string CurrencyCode,
    string ExternalReference,
    string? ProviderCheckoutId,
    string? PaymentUrl,
    string? QrPayload,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? PaidAt,
    string? ProviderPaymentMethod,
    int? SettledPaymentRecordId,
    string? CreatedByName,
    string? FailureReason,
    DateTimeOffset CreatedAt);

public sealed record CreatePaymentLinkCommand(
    decimal? Amount,
    int? ExpiresInHours,
    string? Description,
    string PublicFrontendBaseUrl,
    int? ActorUserId);

public sealed record PaymentOperationResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorMessage,
    int StatusCode = StatusCodes.Status200OK)
{
    public static PaymentOperationResult<T> Ok(T value, int statusCode = StatusCodes.Status200OK)
        => new(true, value, null, statusCode);

    public static PaymentOperationResult<T> Fail(string message, int statusCode = StatusCodes.Status400BadRequest)
        => new(false, default, message, statusCode);
}

public sealed class PaymentCheckoutService
{
    private readonly AppDbContext _context;
    private readonly IPaymentGatewayRegistry _gatewayRegistry;
    private readonly ISecretProtector _secretProtector;
    private readonly ILogger<PaymentCheckoutService> _logger;
    private readonly NotificationDispatcherService? _notificationDispatcher;
    private readonly IConfiguration? _configuration;

    public PaymentCheckoutService(
        AppDbContext context,
        IPaymentGatewayRegistry gatewayRegistry,
        ISecretProtector secretProtector,
        ILogger<PaymentCheckoutService> logger,
        NotificationDispatcherService? notificationDispatcher = null,
        IConfiguration? configuration = null)
    {
        _context = context;
        _gatewayRegistry = gatewayRegistry;
        _secretProtector = secretProtector;
        _logger = logger;
        _notificationDispatcher = notificationDispatcher;
        _configuration = configuration;
    }

    public static (bool Allowed, string? Reason) EvaluateWorkspaceEligibility(Shop workspace)
    {
        if (workspace.OperatingMode == OperatingMode.InternalMaintenance)
        {
            return (false, "Online payments are unavailable in Internal Maintenance workspaces.");
        }

        var featureConfig = IndustryBlueprintCatalog.DeserializeFeatureConfig(
            workspace.FeatureConfig,
            workspace.IndustryBlueprintKey,
            workspace.OperatingMode);

        if (!featureConfig.ShowBillingAndPayments)
        {
            return (false, "Billing & Payments is disabled for this workspace.");
        }

        return (true, null);
    }

    public async Task<WorkspacePaymentGateway> GetOrCreateGatewayConfigAsync(int shopId, CancellationToken cancellationToken)
    {
        var existing = await _context.WorkspacePaymentGateways
            .FirstOrDefaultAsync(x => x.ShopId == shopId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new WorkspacePaymentGateway
        {
            ShopId = shopId,
            Provider = PaymentGatewayProvider.None,
            Environment = PaymentGatewayEnvironment.Sandbox,
            IsEnabled = false,
            WebhookToken = GenerateWebhookToken(),
            DefaultLinkExpiryHours = 24,
            EnablePublicTrackingPayment = false,
            RequirePhoneVerificationForPublicPayment = true
        };

        _context.WorkspacePaymentGateways.Add(created);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            _context.Entry(created).State = EntityState.Detached;
            return await _context.WorkspacePaymentGateways
                .FirstAsync(x => x.ShopId == shopId, cancellationToken);
        }
    }

    public static string GenerateWebhookToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static string ExtractLast4(string secret)
    {
        var trimmed = secret.Trim();
        return trimmed.Length <= 4 ? trimmed : trimmed[^4..];
    }

    public GatewayCredentials? TryDecryptCredentials(WorkspacePaymentGateway config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKeyEncrypted))
        {
            return null;
        }

        var apiKey = _secretProtector.Unprotect(config.ApiKeyEncrypted);
        var webhookSecret = string.IsNullOrWhiteSpace(config.WebhookSecretEncrypted)
            ? null
            : _secretProtector.Unprotect(config.WebhookSecretEncrypted);

        return new GatewayCredentials(apiKey, webhookSecret, config.Environment);
    }

    public async Task<PaymentOperationResult<PaymentLinkDto>> CreatePaymentLinkAsync(
        int shopId,
        int ticketId,
        CreatePaymentLinkCommand command,
        CancellationToken cancellationToken)
    {
        var workspace = await _context.Shops.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == shopId, cancellationToken);
        if (workspace is null)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Workspace not found.", StatusCodes.Status404NotFound);
        }

        var (allowed, eligibilityReason) = EvaluateWorkspaceEligibility(workspace);
        if (!allowed)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail(eligibilityReason!, StatusCodes.Status400BadRequest);
        }

        var gatewayConfig = await _context.WorkspacePaymentGateways.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShopId == shopId, cancellationToken);
        if (gatewayConfig is null || !gatewayConfig.IsEnabled || gatewayConfig.Provider == PaymentGatewayProvider.None)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Online payment gateway is not enabled for this workspace.");
        }

        var gateway = _gatewayRegistry.Resolve(gatewayConfig.Provider);
        if (gateway is null)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail($"Payment provider '{gatewayConfig.Provider}' is not available.");
        }

        GatewayCredentials? credentials;
        try
        {
            credentials = TryDecryptCredentials(gatewayConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt payment gateway credentials for workspace {ShopId}.", shopId);
            return PaymentOperationResult<PaymentLinkDto>.Fail("Payment gateway credentials could not be decrypted. Please re-save your API key in Settings.");
        }

        if (credentials is null || string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Payment gateway API key is not configured.");
        }

        var ticket = await _context.ServiceTickets
            .Include(x => x.Status)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == ticketId && x.ShopId == shopId && x.DeletedAt == null, cancellationToken);
        if (ticket is null)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Service ticket not found.", StatusCodes.Status404NotFound);
        }

        if (ticket.IsInternalWorkOrder)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Online payment links cannot be created for internal work orders.");
        }

        if (ticket.Status.IsCancelled)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Cannot create a payment link for a cancelled ticket.");
        }

        if (ticket.PaymentStatus == PaymentStatus.Waived)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Cannot create a payment link for a waived ticket.");
        }

        var billing = await TicketBillingService.GetBillingSnapshotAsync(_context, shopId, ticket, cancellationToken);
        decimal targetAmount;
        if (command.Amount.HasValue)
        {
            targetAmount = TicketBillingCalculator.RoundMoney(command.Amount.Value);
            if (targetAmount <= 0m)
            {
                return PaymentOperationResult<PaymentLinkDto>.Fail("Payment amount must be greater than zero.");
            }

            if (billing.TotalAmount.HasValue)
            {
                if (billing.RemainingBalance <= 0m)
                {
                    return PaymentOperationResult<PaymentLinkDto>.Fail("This ticket has no remaining balance to collect.");
                }

                if (targetAmount > billing.RemainingBalance)
                {
                    return PaymentOperationResult<PaymentLinkDto>.Fail(
                        $"Payment amount ({targetAmount:0.##}) cannot exceed the remaining balance ({billing.RemainingBalance:0.##}).");
                }
            }
        }
        else
        {
            if (billing.RemainingBalance <= 0m)
            {
                return PaymentOperationResult<PaymentLinkDto>.Fail("This ticket has no remaining balance. Enter a custom deposit amount or set a ticket total first.");
            }

            targetAmount = billing.RemainingBalance;
        }

        var currencyCode = string.IsNullOrWhiteSpace(workspace.CurrencyCode) ? "IDR" : workspace.CurrencyCode.Trim().ToUpperInvariant();
        var expiryHours = Math.Clamp(command.ExpiresInHours ?? gatewayConfig.DefaultLinkExpiryHours, 1, 168);
        var description = string.IsNullOrWhiteSpace(command.Description)
            ? $"{workspace.Name} - {ticket.TicketNumber}"
            : command.Description.Trim();
        if (description.Length > 200)
        {
            description = description[..200];
        }

        var baseUrl = command.PublicFrontendBaseUrl.TrimEnd('/');
        var redirectPath = ticket.PublicTrackingEnabled && !string.IsNullOrWhiteSpace(ticket.PublicTrackingToken)
            ? $"/track/{ticket.PublicTrackingToken}"
            : $"/app/tickets/{ticket.Id}";
        var successUrl = $"{baseUrl}{redirectPath}?payment=success";
        var cancelUrl = $"{baseUrl}{redirectPath}?payment=cancelled";

        var externalReference = $"srv-{shopId}-{ticket.Id}-{Guid.NewGuid().ToString("N")[..12]}";
        var createRequest = new GatewayCreateCheckoutRequest(
            ExternalReference: externalReference,
            Amount: targetAmount,
            CurrencyCode: currencyCode,
            Description: description,
            CustomerName: !string.IsNullOrWhiteSpace(ticket.CustomerNameSnapshot) ? ticket.CustomerNameSnapshot : ticket.Customer.FullName,
            CustomerEmail: ticket.Customer.Email,
            CustomerPhone: !string.IsNullOrWhiteSpace(ticket.CustomerPhoneSnapshot) ? ticket.CustomerPhoneSnapshot : ticket.Customer.PhoneNumber,
            SuccessRedirectUrl: successUrl,
            CancelRedirectUrl: cancelUrl,
            ExpiresInHours: expiryHours,
            Metadata: new Dictionary<string, string>
            {
                ["workspace_id"] = shopId.ToString(CultureInfo.InvariantCulture),
                ["ticket_id"] = ticket.Id.ToString(CultureInfo.InvariantCulture),
                ["ticket_number"] = ticket.TicketNumber,
                ["external_reference"] = externalReference
            });

        GatewayCreateCheckoutResult gatewayResult;
        try
        {
            gatewayResult = await gateway.CreateCheckoutAsync(credentials, createRequest, cancellationToken);
        }
        catch (PaymentGatewayException ex)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail(ex.Message, StatusCodes.Status502BadGateway);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected payment gateway error while creating checkout for ticket {TicketId}.", ticketId);
            return PaymentOperationResult<PaymentLinkDto>.Fail("Failed to communicate with the payment gateway.", StatusCodes.Status502BadGateway);
        }

        var supersededProviderIds = new List<string>();
        PaymentCheckoutIntent newIntent;

        await using (var tx = await _context.Database.BeginTransactionAsync(cancellationToken))
        {
            var existingPending = await _context.PaymentCheckoutIntents
                .Where(x => x.ShopId == shopId && x.ServiceTicketId == ticket.Id && x.Status == PaymentCheckoutStatus.Pending)
                .ToListAsync(cancellationToken);

            foreach (var oldIntent in existingPending)
            {
                oldIntent.Status = PaymentCheckoutStatus.Superseded;
                if (!string.IsNullOrWhiteSpace(oldIntent.ProviderCheckoutId) && oldIntent.Provider == gatewayConfig.Provider)
                {
                    supersededProviderIds.Add(oldIntent.ProviderCheckoutId);
                }
            }

            newIntent = new PaymentCheckoutIntent
            {
                ShopId = shopId,
                ServiceTicketId = ticket.Id,
                Provider = gatewayConfig.Provider,
                Environment = gatewayConfig.Environment,
                Status = PaymentCheckoutStatus.Pending,
                Amount = targetAmount,
                CurrencyCode = currencyCode,
                ExternalReference = externalReference,
                ProviderCheckoutId = gatewayResult.ProviderCheckoutId,
                PaymentUrl = gatewayResult.PaymentUrl,
                QrPayload = gatewayResult.QrPayload,
                ExpiresAt = gatewayResult.ExpiresAt,
                CreatedByUserId = command.ActorUserId
            };

            _context.PaymentCheckoutIntents.Add(newIntent);

            var initiatorLabel = command.ActorUserId.HasValue ? "staff" : "customer tracking page";
            _context.ServiceTicketHistories.Add(new ServiceTicketHistory
            {
                ShopId = shopId,
                ServiceTicketId = ticket.Id,
                ActorUserId = command.ActorUserId,
                EventType = TicketHistoryEventType.PaymentUpdated,
                Note = $"Online payment link created via {gatewayConfig.Provider} ({targetAmount:0.##} {currencyCode}) by {initiatorLabel}.",
                IsPublicVisible = false,
                CreatedAt = DateTimeOffset.UtcNow
            });

            _context.ActivityLogs.Add(new ActivityLog
            {
                ShopId = shopId,
                ActorUserId = command.ActorUserId,
                EntityType = "ServiceTicket",
                EntityId = ticket.Id,
                ActionType = "PaymentLinkCreated",
                Title = $"Payment link created for {ticket.TicketNumber}",
                Description = $"{gatewayConfig.Provider} ({gatewayConfig.Environment}) link for {targetAmount:0.##} {currencyCode}",
                MetadataJson = JsonSerializer.Serialize(new
                {
                    provider = gatewayConfig.Provider.ToString(),
                    amount = targetAmount,
                    currencyCode,
                    externalReference,
                    supersededCount = existingPending.Count
                })
            });

            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        // Best-effort expire superseded checkout links at provider outside DB lock.
        foreach (var oldProviderId in supersededProviderIds)
        {
            await gateway.CancelCheckoutAsync(credentials, oldProviderId, cancellationToken);
        }

        var dto = await ProjectIntentDtoAsync(shopId, newIntent.Id, cancellationToken);
        return PaymentOperationResult<PaymentLinkDto>.Ok(dto!, StatusCodes.Status201Created);
    }

    public async Task<PaymentOperationResult<PaymentLinkDto>> SyncIntentStatusAsync(
        int shopId,
        int ticketId,
        int intentId,
        int? actorUserId,
        CancellationToken cancellationToken)
    {
        var intent = await _context.PaymentCheckoutIntents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == intentId && x.ShopId == shopId && x.ServiceTicketId == ticketId, cancellationToken);
        if (intent is null)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Payment link not found.", StatusCodes.Status404NotFound);
        }

        if (intent.Status == PaymentCheckoutStatus.Paid)
        {
            var existingDto = await ProjectIntentDtoAsync(shopId, intent.Id, cancellationToken);
            return PaymentOperationResult<PaymentLinkDto>.Ok(existingDto!);
        }

        if (string.IsNullOrWhiteSpace(intent.ProviderCheckoutId))
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Payment link does not have a provider checkout ID.");
        }

        var gatewayConfig = await _context.WorkspacePaymentGateways.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShopId == shopId, cancellationToken);
        if (gatewayConfig is null || gatewayConfig.Provider != intent.Provider)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Active workspace gateway configuration does not match this payment link's provider.");
        }

        var gateway = _gatewayRegistry.Resolve(intent.Provider);
        if (gateway is null)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail($"Payment provider '{intent.Provider}' is unavailable.");
        }

        var credentials = TryDecryptCredentials(gatewayConfig);
        if (credentials is null)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Payment gateway API key is not configured.");
        }

        GatewayCheckoutStatusResult statusResult;
        try
        {
            statusResult = await gateway.GetCheckoutStatusAsync(credentials, intent.ProviderCheckoutId, cancellationToken);
        }
        catch (PaymentGatewayException ex)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail(ex.Message, StatusCodes.Status502BadGateway);
        }

        if (statusResult.Status != PaymentCheckoutStatus.Pending)
        {
            await ApplyProviderStatusAsync(
                shopId,
                intent.Id,
                statusResult.Status,
                statusResult.ProviderPaymentMethod,
                statusResult.PaidAt,
                statusResult.FailureReason,
                actorUserId,
                source: "manual sync",
                cancellationToken);
        }

        var dto = await ProjectIntentDtoAsync(shopId, intent.Id, cancellationToken);
        return PaymentOperationResult<PaymentLinkDto>.Ok(dto!);
    }

    public async Task<PaymentOperationResult<PaymentLinkDto>> CancelIntentAsync(
        int shopId,
        int ticketId,
        int intentId,
        int? actorUserId,
        CancellationToken cancellationToken)
    {
        var intent = await _context.PaymentCheckoutIntents
            .Include(x => x.ServiceTicket)
            .FirstOrDefaultAsync(x => x.Id == intentId && x.ShopId == shopId && x.ServiceTicketId == ticketId, cancellationToken);
        if (intent is null)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail("Payment link not found.", StatusCodes.Status404NotFound);
        }

        if (intent.Status != PaymentCheckoutStatus.Pending)
        {
            return PaymentOperationResult<PaymentLinkDto>.Fail($"Only pending payment links can be cancelled (current status: {intent.Status}).");
        }

        intent.Status = PaymentCheckoutStatus.Cancelled;

        _context.ServiceTicketHistories.Add(new ServiceTicketHistory
        {
            ShopId = shopId,
            ServiceTicketId = ticketId,
            ActorUserId = actorUserId,
            EventType = TicketHistoryEventType.PaymentUpdated,
            Note = $"Online payment link ({intent.ExternalReference}) cancelled.",
            IsPublicVisible = false,
            CreatedAt = DateTimeOffset.UtcNow
        });

        _context.ActivityLogs.Add(new ActivityLog
        {
            ShopId = shopId,
            ActorUserId = actorUserId,
            EntityType = "ServiceTicket",
            EntityId = ticketId,
            ActionType = "PaymentLinkCancelled",
            Title = $"Payment link cancelled for {intent.ServiceTicket.TicketNumber}",
            Description = $"Cancelled {intent.Provider} link ({intent.ExternalReference})"
        });

        await _context.SaveChangesAsync(cancellationToken);

        // Best-effort expire at provider
        if (!string.IsNullOrWhiteSpace(intent.ProviderCheckoutId))
        {
            var gatewayConfig = await _context.WorkspacePaymentGateways.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShopId == shopId, cancellationToken);
            var gateway = _gatewayRegistry.Resolve(intent.Provider);
            if (gatewayConfig is not null && gateway is not null && gatewayConfig.Provider == intent.Provider)
            {
                try
                {
                    var creds = TryDecryptCredentials(gatewayConfig);
                    if (creds is not null)
                    {
                        await gateway.CancelCheckoutAsync(creds, intent.ProviderCheckoutId, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Best-effort cancel failed for intent {IntentId}.", intentId);
                }
            }
        }

        var dto = await ProjectIntentDtoAsync(shopId, intent.Id, cancellationToken);
        return PaymentOperationResult<PaymentLinkDto>.Ok(dto!);
    }

    public async Task<bool> ApplyProviderStatusAsync(
        int shopId,
        int intentId,
        PaymentCheckoutStatus targetStatus,
        string? providerPaymentMethod,
        DateTimeOffset? paidAt,
        string? failureReason,
        int? actorUserId,
        string source,
        CancellationToken cancellationToken)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        var intent = await _context.PaymentCheckoutIntents
            .Include(x => x.ServiceTicket)
                .ThenInclude(t => t.Customer)
            .FirstOrDefaultAsync(x => x.Id == intentId && x.ShopId == shopId, cancellationToken);

        if (intent is null)
        {
            return false;
        }

        if (targetStatus == PaymentCheckoutStatus.Paid)
        {
            if (intent.Status == PaymentCheckoutStatus.Paid || intent.SettledPaymentRecordId.HasValue)
            {
                return false;
            }

            var previousIntentStatus = intent.Status;
            var wasNonPending = previousIntentStatus != PaymentCheckoutStatus.Pending;
            var settledAt = paidAt ?? DateTimeOffset.UtcNow;

            intent.Status = PaymentCheckoutStatus.Paid;
            intent.PaidAt = settledAt;
            intent.ProviderPaymentMethod = string.IsNullOrWhiteSpace(providerPaymentMethod)
                ? intent.ProviderPaymentMethod
                : Truncate(providerPaymentMethod.Trim(), 80);
            intent.FailureReason = null;

            var methodLabel = string.IsNullOrWhiteSpace(intent.ProviderPaymentMethod)
                ? intent.Provider.ToString()
                : Truncate($"{intent.Provider} ({intent.ProviderPaymentMethod})", 80);

            var notes = wasNonPending
                ? $"Settled online via {source} (late payment received on {previousIntentStatus} link {intent.ExternalReference})."
                : $"Settled online via {source} ({intent.ExternalReference}).";

            var paymentRecord = new PaymentRecord
            {
                ShopId = shopId,
                ServiceTicketId = intent.ServiceTicketId,
                EntryType = PaymentEntryType.Payment,
                Status = intent.ServiceTicket.PaymentStatus,
                TransactionAmount = intent.Amount,
                AmountPaid = intent.ServiceTicket.AmountPaid,
                TotalAmount = intent.ServiceTicket.TotalAmount,
                Method = methodLabel,
                ReferenceNumber = Truncate(intent.ProviderCheckoutId ?? intent.ExternalReference, 120),
                Notes = Truncate(notes, 1000),
                UpdatedByUserId = null,
                RecordedAt = settledAt,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.PaymentRecords.Add(paymentRecord);
            await _context.SaveChangesAsync(cancellationToken);

            var billing = await TicketBillingService.RecalculateTicketBillingAsync(
                _context,
                shopId,
                intent.ServiceTicket,
                actorUserId,
                reasonNote: $"Online payment ({intent.Provider})",
                forceFormulaWhenZero: false,
                logStatusTransitionHistory: true,
                cancellationToken: cancellationToken);

            paymentRecord.Status = billing.PaymentStatus;
            paymentRecord.AmountPaid = billing.AmountPaid;
            paymentRecord.TotalAmount = billing.TotalAmount;
            intent.SettledPaymentRecordId = paymentRecord.Id;

            var historyMessage = $"Online payment of {intent.Amount:0.##} {intent.CurrencyCode} settled via {methodLabel} (Ref: {paymentRecord.ReferenceNumber}).";
            if (wasNonPending)
            {
                historyMessage += $" Received on a {previousIntentStatus} payment link.";
            }
            if (billing.OverpaidAmount > 0m)
            {
                historyMessage += $" OVERPAYMENT ALERT: Ticket is overpaid by {billing.OverpaidAmount:0.##} {intent.CurrencyCode}. Please review and process a manual refund if needed.";
            }

            _context.ServiceTicketHistories.Add(new ServiceTicketHistory
            {
                ShopId = shopId,
                ServiceTicketId = intent.ServiceTicketId,
                ActorUserId = actorUserId,
                EventType = TicketHistoryEventType.PaymentUpdated,
                Note = historyMessage,
                IsPublicVisible = false,
                CreatedAt = DateTimeOffset.UtcNow
            });

            _context.ActivityLogs.Add(new ActivityLog
            {
                ShopId = shopId,
                ActorUserId = actorUserId,
                EntityType = "ServiceTicket",
                EntityId = intent.ServiceTicketId,
                ActionType = "OnlinePaymentSettled",
                Title = $"Online payment received for {intent.ServiceTicket.TicketNumber}",
                Description = historyMessage,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    intentId = intent.Id,
                    paymentRecordId = paymentRecord.Id,
                    provider = intent.Provider.ToString(),
                    amount = intent.Amount,
                    currencyCode = intent.CurrencyCode,
                    overpaidAmount = billing.OverpaidAmount,
                    source
                })
            });

            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            if (_notificationDispatcher is not null)
            {
                var frontendBaseUrl = (_configuration?["PublicFrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
                var trackUrl = intent.ServiceTicket.PublicTrackingEnabled
                    ? $"{frontendBaseUrl}/track/{intent.ServiceTicket.PublicTrackingToken}"
                    : $"{frontendBaseUrl}/app/tickets/{intent.ServiceTicketId}";
                await _notificationDispatcher.PublishEventAsync(new NotificationEventEnvelope(
                    ShopId: shopId,
                    EventType: NotificationEventType.PaymentSettled,
                    Title: $"Payment received for {intent.ServiceTicket.TicketNumber}",
                    Message: $"Online payment of {intent.Amount:0.##} {intent.CurrencyCode} via {methodLabel} has been settled for ticket {intent.ServiceTicket.TicketNumber}.",
                    DeepLinkUrl: trackUrl,
                    CustomerName: intent.ServiceTicket.Customer?.FullName,
                    CustomerEmail: intent.ServiceTicket.Customer?.Email,
                    CustomerPhone: intent.ServiceTicket.Customer?.PhoneNumber,
                    ReferenceEntityType: "ServiceTicket",
                    ReferenceEntityId: intent.ServiceTicketId,
                    Data: new { ticketId = intent.ServiceTicketId, ticketNumber = intent.ServiceTicket.TicketNumber, amount = intent.Amount, currencyCode = intent.CurrencyCode, provider = intent.Provider.ToString() }),
                    cancellationToken);
            }

            return true;
        }

        if (intent.Status != PaymentCheckoutStatus.Pending)
        {
            return false;
        }

        if (targetStatus is PaymentCheckoutStatus.Expired or PaymentCheckoutStatus.Failed or PaymentCheckoutStatus.Cancelled)
        {
            intent.Status = targetStatus;
            intent.FailureReason = string.IsNullOrWhiteSpace(failureReason)
                ? intent.FailureReason
                : Truncate(failureReason.Trim(), 500);
            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<PaymentLinkDto?> ProjectIntentDtoAsync(int shopId, int intentId, CancellationToken cancellationToken)
    {
        var row = await _context.PaymentCheckoutIntents.AsNoTracking()
            .Where(x => x.Id == intentId && x.ShopId == shopId)
            .Select(x => new
            {
                x.Id,
                x.ServiceTicketId,
                x.Provider,
                x.Environment,
                x.Status,
                x.Amount,
                x.CurrencyCode,
                x.ExternalReference,
                x.ProviderCheckoutId,
                x.PaymentUrl,
                x.QrPayload,
                x.ExpiresAt,
                x.PaidAt,
                x.ProviderPaymentMethod,
                x.SettledPaymentRecordId,
                CreatedByName = x.CreatedByUser == null ? null : (x.CreatedByUser.FirstName + " " + x.CreatedByUser.LastName).Trim(),
                x.FailureReason,
                x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new PaymentLinkDto(
                row.Id,
                row.ServiceTicketId,
                row.Provider.ToString(),
                row.Environment.ToString(),
                row.Status.ToString(),
                row.Amount,
                row.CurrencyCode,
                row.ExternalReference,
                row.ProviderCheckoutId,
                row.PaymentUrl,
                row.QrPayload,
                row.ExpiresAt,
                row.PaidAt,
                row.ProviderPaymentMethod,
                row.SettledPaymentRecordId,
                string.IsNullOrWhiteSpace(row.CreatedByName) ? null : row.CreatedByName,
                row.FailureReason,
                row.CreatedAt);
    }

    public async Task<IReadOnlyList<PaymentLinkDto>> ListTicketLinksAsync(int shopId, int ticketId, CancellationToken cancellationToken)
    {
        var rows = await _context.PaymentCheckoutIntents.AsNoTracking()
            .Where(x => x.ShopId == shopId && x.ServiceTicketId == ticketId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ServiceTicketId,
                x.Provider,
                x.Environment,
                x.Status,
                x.Amount,
                x.CurrencyCode,
                x.ExternalReference,
                x.ProviderCheckoutId,
                x.PaymentUrl,
                x.QrPayload,
                x.ExpiresAt,
                x.PaidAt,
                x.ProviderPaymentMethod,
                x.SettledPaymentRecordId,
                CreatedByName = x.CreatedByUser == null ? null : (x.CreatedByUser.FirstName + " " + x.CreatedByUser.LastName).Trim(),
                x.FailureReason,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new PaymentLinkDto(
            row.Id,
            row.ServiceTicketId,
            row.Provider.ToString(),
            row.Environment.ToString(),
            row.Status.ToString(),
            row.Amount,
            row.CurrencyCode,
            row.ExternalReference,
            row.ProviderCheckoutId,
            row.PaymentUrl,
            row.QrPayload,
            row.ExpiresAt,
            row.PaidAt,
            row.ProviderPaymentMethod,
            row.SettledPaymentRecordId,
            string.IsNullOrWhiteSpace(row.CreatedByName) ? null : row.CreatedByName,
            row.FailureReason,
            row.CreatedAt)).ToList();
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
