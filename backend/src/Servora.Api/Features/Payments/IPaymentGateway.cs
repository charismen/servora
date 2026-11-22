namespace Servora.Api.Features.Payments;

public sealed record GatewayCredentials(
    string ApiKey,
    string? WebhookSecret,
    PaymentGatewayEnvironment Environment);

public sealed record GatewayCreateCheckoutRequest(
    string ExternalReference,
    decimal Amount,
    string CurrencyCode,
    string Description,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string SuccessRedirectUrl,
    string CancelRedirectUrl,
    int ExpiresInHours,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record GatewayCreateCheckoutResult(
    string ProviderCheckoutId,
    string PaymentUrl,
    string? QrPayload,
    DateTimeOffset ExpiresAt);

public sealed record GatewayCheckoutStatusResult(
    PaymentCheckoutStatus Status,
    string? ProviderPaymentMethod,
    DateTimeOffset? PaidAt,
    string? FailureReason);

public sealed record GatewayTestConnectionResult(
    bool Success,
    string Message);

public sealed record GatewayWebhookVerificationResult(
    bool IsValid,
    string? ErrorMessage,
    string? ProviderEventId,
    string? EventType,
    string? ProviderCheckoutId,
    string? ExternalReference,
    PaymentCheckoutStatus? MappedStatus,
    string? ProviderPaymentMethod,
    DateTimeOffset? PaidAt,
    string? FailureReason);

public sealed class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public interface IPaymentGateway
{
    PaymentGatewayProvider Provider { get; }

    Task<GatewayCreateCheckoutResult> CreateCheckoutAsync(
        GatewayCredentials credentials,
        GatewayCreateCheckoutRequest request,
        CancellationToken cancellationToken);

    Task<GatewayCheckoutStatusResult> GetCheckoutStatusAsync(
        GatewayCredentials credentials,
        string providerCheckoutId,
        CancellationToken cancellationToken);

    Task CancelCheckoutAsync(
        GatewayCredentials credentials,
        string providerCheckoutId,
        CancellationToken cancellationToken);

    Task<GatewayTestConnectionResult> TestConnectionAsync(
        GatewayCredentials credentials,
        CancellationToken cancellationToken);

    Task<GatewayWebhookVerificationResult> VerifyAndParseWebhookAsync(
        GatewayCredentials credentials,
        HttpRequest httpRequest,
        string rawBody,
        CancellationToken cancellationToken);
}

public interface IPaymentGatewayRegistry
{
    IPaymentGateway? Resolve(PaymentGatewayProvider provider);
}

public sealed class PaymentGatewayRegistry : IPaymentGatewayRegistry
{
    private readonly IReadOnlyDictionary<PaymentGatewayProvider, IPaymentGateway> _gateways;

    public PaymentGatewayRegistry(IEnumerable<IPaymentGateway> gateways)
    {
        _gateways = gateways.ToDictionary(gateway => gateway.Provider);
    }

    public IPaymentGateway? Resolve(PaymentGatewayProvider provider)
        => _gateways.TryGetValue(provider, out var gateway) ? gateway : null;
}
