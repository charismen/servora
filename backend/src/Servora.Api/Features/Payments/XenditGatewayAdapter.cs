using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Servora.Api.Features.Payments;

public sealed class XenditGatewayAdapter : IPaymentGateway
{
    public const string HttpClientName = "PaymentGatewayXendit";
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "IDR", "PHP", "USD", "SGD", "MYR", "VND", "THB"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<XenditGatewayAdapter> _logger;

    public PaymentGatewayProvider Provider => PaymentGatewayProvider.Xendit;

    public XenditGatewayAdapter(
        IHttpClientFactory httpClientFactory,
        ILogger<XenditGatewayAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GatewayCreateCheckoutResult> CreateCheckoutAsync(
        GatewayCredentials credentials,
        GatewayCreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (!SupportedCurrencies.Contains(currency))
        {
            throw new PaymentGatewayException($"Currency '{currency}' is not supported by Xendit invoices.");
        }

        var durationSeconds = Math.Clamp(request.ExpiresInHours, 1, 168) * 3600;
        var payload = new Dictionary<string, object?>
        {
            ["external_id"] = request.ExternalReference,
            ["amount"] = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            ["currency"] = currency,
            ["invoice_duration"] = durationSeconds,
            ["description"] = request.Description,
            ["success_redirect_url"] = request.SuccessRedirectUrl,
            ["failure_redirect_url"] = request.CancelRedirectUrl
        };

        var customerObj = BuildCustomerPayload(request);
        if (customerObj is not null)
        {
            payload["customer"] = customerObj;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v2/invoices")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        ApplyBasicAuth(httpRequest, credentials.ApiKey);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentGatewayException($"Xendit invoice creation failed ({(int)response.StatusCode}): {ExtractErrorMessage(body)}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var invoiceId = GetStringProperty(root, "id");
        var invoiceUrl = GetStringProperty(root, "invoice_url");
        if (string.IsNullOrWhiteSpace(invoiceId) || string.IsNullOrWhiteSpace(invoiceUrl))
        {
            throw new PaymentGatewayException("Xendit returned an incomplete invoice response.");
        }

        var expiryDate = TryGetDateTimeOffset(root, "expiry_date")
            ?? DateTimeOffset.UtcNow.AddSeconds(durationSeconds);

        return new GatewayCreateCheckoutResult(
            invoiceId,
            invoiceUrl,
            QrPayload: null,
            ExpiresAt: expiryDate);
    }

    public async Task<GatewayCheckoutStatusResult> GetCheckoutStatusAsync(
        GatewayCredentials credentials,
        string providerCheckoutId,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"v2/invoices/{Uri.EscapeDataString(providerCheckoutId)}");
        ApplyBasicAuth(httpRequest, credentials.ApiKey);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentGatewayException($"Xendit status check failed ({(int)response.StatusCode}): {ExtractErrorMessage(body)}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var rawStatus = GetStringProperty(root, "status");
        var mappedStatus = MapXenditStatus(rawStatus);
        var paymentMethod = FormatPaymentMethod(root);
        var paidAt = TryGetDateTimeOffset(root, "paid_at");
        var failureReason = mappedStatus == PaymentCheckoutStatus.Failed
            ? $"Xendit reported status '{rawStatus}'."
            : null;

        return new GatewayCheckoutStatusResult(mappedStatus, paymentMethod, paidAt, failureReason);
    }

    public async Task CancelCheckoutAsync(
        GatewayCredentials credentials,
        string providerCheckoutId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"invoices/{Uri.EscapeDataString(providerCheckoutId)}/expire!");
            ApplyBasicAuth(httpRequest, credentials.ApiKey);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Best-effort Xendit invoice expiry failed for {InvoiceId} with HTTP {StatusCode}.",
                    providerCheckoutId,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Best-effort Xendit invoice expiry failed for {InvoiceId}.", providerCheckoutId);
        }
    }

    public async Task<GatewayTestConnectionResult> TestConnectionAsync(
        GatewayCredentials credentials,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "v2/invoices?limit=1");
            ApplyBasicAuth(httpRequest, credentials.ApiKey);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new GatewayTestConnectionResult(true, "Xendit credentials verified successfully.");
            }

            return new GatewayTestConnectionResult(
                false,
                $"Xendit rejected the API key ({(int)response.StatusCode}): {ExtractErrorMessage(body)}");
        }
        catch (Exception ex)
        {
            return new GatewayTestConnectionResult(false, $"Could not reach Xendit API: {ex.Message}");
        }
    }

    public Task<GatewayWebhookVerificationResult> VerifyAndParseWebhookAsync(
        GatewayCredentials credentials,
        HttpRequest httpRequest,
        string rawBody,
        CancellationToken cancellationToken)
    {
        var callbackToken = httpRequest.Headers["x-callback-token"].FirstOrDefault();
        var webhookId = httpRequest.Headers["webhook-id"].FirstOrDefault();
        return Task.FromResult(VerifyAndParseWebhook(credentials.WebhookSecret, callbackToken, webhookId, rawBody));
    }

    public static GatewayWebhookVerificationResult VerifyAndParseWebhook(
        string? configuredWebhookSecret,
        string? callbackTokenHeader,
        string? webhookIdHeader,
        string rawBody)
    {
        if (string.IsNullOrWhiteSpace(configuredWebhookSecret))
        {
            return Invalid("Workspace Xendit webhook verification token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(callbackTokenHeader))
        {
            return Invalid("Missing x-callback-token header.");
        }

        var expectedBytes = Encoding.UTF8.GetBytes(configuredWebhookSecret.Trim());
        var actualBytes = Encoding.UTF8.GetBytes(callbackTokenHeader.Trim());
        if (expectedBytes.Length != actualBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            return Invalid("Invalid x-callback-token signature.");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var invoiceId = GetStringProperty(root, "id");
            var externalId = GetStringProperty(root, "external_id");
            var rawStatus = GetStringProperty(root, "status") ?? "UNKNOWN";
            var paidAt = TryGetDateTimeOffset(root, "paid_at");
            var updated = GetStringProperty(root, "updated");
            var paymentMethod = FormatPaymentMethod(root);
            var mappedStatus = MapXenditStatus(rawStatus);

            var eventId = !string.IsNullOrWhiteSpace(webhookIdHeader)
                ? webhookIdHeader.Trim()
                : $"xendit:{invoiceId ?? externalId ?? "unknown"}:{rawStatus.ToUpperInvariant()}:{updated ?? paidAt?.ToUnixTimeSeconds().ToString() ?? "0"}";

            return new GatewayWebhookVerificationResult(
                IsValid: true,
                ErrorMessage: null,
                ProviderEventId: eventId,
                EventType: $"invoice.{rawStatus.ToLowerInvariant()}",
                ProviderCheckoutId: invoiceId,
                ExternalReference: externalId,
                MappedStatus: mappedStatus,
                ProviderPaymentMethod: paymentMethod,
                PaidAt: paidAt,
                FailureReason: mappedStatus == PaymentCheckoutStatus.Failed ? $"Xendit reported status '{rawStatus}'." : null);
        }
        catch (JsonException ex)
        {
            return Invalid($"Invalid Xendit webhook JSON payload: {ex.Message}");
        }
    }

    public static PaymentCheckoutStatus MapXenditStatus(string? rawStatus)
        => rawStatus?.Trim().ToUpperInvariant() switch
        {
            "PAID" or "SETTLED" => PaymentCheckoutStatus.Paid,
            "EXPIRED" => PaymentCheckoutStatus.Expired,
            "PENDING" => PaymentCheckoutStatus.Pending,
            _ => PaymentCheckoutStatus.Failed
        };

    private static void ApplyBasicAuth(HttpRequestMessage request, string apiKey)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private static Dictionary<string, object?>? BuildCustomerPayload(GatewayCreateCheckoutRequest request)
    {
        var customer = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(request.CustomerName))
        {
            customer["given_names"] = request.CustomerName.Trim();
        }
        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            customer["email"] = request.CustomerEmail.Trim();
        }
        if (!string.IsNullOrWhiteSpace(request.CustomerPhone))
        {
            customer["mobile_number"] = request.CustomerPhone.Trim();
        }

        return customer.Count > 0 ? customer : null;
    }

    private static string? FormatPaymentMethod(JsonElement root)
    {
        var method = GetStringProperty(root, "payment_method");
        var channel = GetStringProperty(root, "payment_channel");
        if (!string.IsNullOrWhiteSpace(channel) && !string.IsNullOrWhiteSpace(method)
            && !string.Equals(channel, method, StringComparison.OrdinalIgnoreCase))
        {
            return $"{method} - {channel}";
        }

        return !string.IsNullOrWhiteSpace(channel) ? channel : method;
    }

    private static string? GetStringProperty(JsonElement root, string name)
        => root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement root, string name)
    {
        var raw = GetStringProperty(root, name);
        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static string ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "Empty response";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
        }

        return body.Length > 240 ? body[..240] : body;
    }

    private static GatewayWebhookVerificationResult Invalid(string message)
        => new(false, message, null, null, null, null, null, null, null, null);
}
