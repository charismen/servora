using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Servora.Api.Features.Payments;

public sealed class StripeGatewayAdapter : IPaymentGateway
{
    public const string HttpClientName = "PaymentGatewayStripe";
    public static readonly TimeSpan DefaultReplayTolerance = TimeSpan.FromMinutes(5);

    // Official Stripe zero-decimal currencies (https://docs.stripe.com/currencies#zero-decimal)
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA",
        "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StripeGatewayAdapter> _logger;

    public PaymentGatewayProvider Provider => PaymentGatewayProvider.Stripe;

    public StripeGatewayAdapter(
        IHttpClientFactory httpClientFactory,
        ILogger<StripeGatewayAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public static long ToStripeMinorUnits(decimal amount, string currencyCode)
    {
        if (amount <= 0)
        {
            throw new PaymentGatewayException("Payment amount must be greater than zero.");
        }

        var currency = currencyCode.Trim().ToUpperInvariant();
        if (ZeroDecimalCurrencies.Contains(currency))
        {
            return (long)decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        }

        return (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
    }

    public async Task<GatewayCreateCheckoutResult> CreateCheckoutAsync(
        GatewayCredentials credentials,
        GatewayCreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var currency = request.CurrencyCode.Trim().ToLowerInvariant();
        var unitAmount = ToStripeMinorUnits(request.Amount, currency);

        // Stripe Checkout Session expires_at must be between 30 minutes and 24 hours from now.
        var durationSeconds = Math.Clamp(request.ExpiresInHours * 3600, 1800, 86400);
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(durationSeconds);

        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("success_url", request.SuccessRedirectUrl),
            new("cancel_url", request.CancelRedirectUrl),
            new("client_reference_id", request.ExternalReference),
            new("expires_at", expiresAt.ToUnixTimeSeconds().ToString()),
            new("line_items[0][quantity]", "1"),
            new("line_items[0][price_data][currency]", currency),
            new("line_items[0][price_data][unit_amount]", unitAmount.ToString()),
            new("line_items[0][price_data][product_data][name]", request.Description)
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            form.Add(new("customer_email", request.CustomerEmail.Trim()));
        }

        if (request.Metadata is not null)
        {
            foreach (var (key, value) in request.Metadata)
            {
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    form.Add(new($"metadata[{key}]", value));
                }
            }
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/checkout/sessions")
        {
            Content = new FormUrlEncodedContent(form)
        };
        ApplyBearerAuth(httpRequest, credentials.ApiKey);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentGatewayException($"Stripe checkout session creation failed ({(int)response.StatusCode}): {ExtractErrorMessage(body)}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var sessionId = GetStringProperty(root, "id");
        var checkoutUrl = GetStringProperty(root, "url");
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(checkoutUrl))
        {
            throw new PaymentGatewayException("Stripe returned an incomplete checkout session response.");
        }

        var providerExpiresAt = TryGetUnixDateTimeOffset(root, "expires_at") ?? expiresAt;
        return new GatewayCreateCheckoutResult(
            sessionId,
            checkoutUrl,
            QrPayload: null,
            ExpiresAt: providerExpiresAt);
    }

    public async Task<GatewayCheckoutStatusResult> GetCheckoutStatusAsync(
        GatewayCredentials credentials,
        string providerCheckoutId,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"v1/checkout/sessions/{Uri.EscapeDataString(providerCheckoutId)}");
        ApplyBearerAuth(httpRequest, credentials.ApiKey);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentGatewayException($"Stripe status check failed ({(int)response.StatusCode}): {ExtractErrorMessage(body)}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var sessionStatus = GetStringProperty(root, "status");
        var paymentStatus = GetStringProperty(root, "payment_status");
        var paymentMethod = ExtractPaymentMethodType(root);

        if (string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return new GatewayCheckoutStatusResult(
                PaymentCheckoutStatus.Paid,
                paymentMethod,
                DateTimeOffset.UtcNow,
                null);
        }

        if (string.Equals(sessionStatus, "expired", StringComparison.OrdinalIgnoreCase))
        {
            return new GatewayCheckoutStatusResult(PaymentCheckoutStatus.Expired, paymentMethod, null, null);
        }

        return new GatewayCheckoutStatusResult(PaymentCheckoutStatus.Pending, paymentMethod, null, null);
    }

    public async Task CancelCheckoutAsync(
        GatewayCredentials credentials,
        string providerCheckoutId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"v1/checkout/sessions/{Uri.EscapeDataString(providerCheckoutId)}/expire");
            ApplyBearerAuth(httpRequest, credentials.ApiKey);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Best-effort Stripe checkout session expiry failed for {SessionId} with HTTP {StatusCode}.",
                    providerCheckoutId,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Best-effort Stripe checkout session expiry failed for {SessionId}.", providerCheckoutId);
        }
    }

    public async Task<GatewayTestConnectionResult> TestConnectionAsync(
        GatewayCredentials credentials,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "v1/balance");
            ApplyBearerAuth(httpRequest, credentials.ApiKey);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new GatewayTestConnectionResult(true, "Stripe credentials verified successfully.");
            }

            return new GatewayTestConnectionResult(
                false,
                $"Stripe rejected the API key ({(int)response.StatusCode}): {ExtractErrorMessage(body)}");
        }
        catch (Exception ex)
        {
            return new GatewayTestConnectionResult(false, $"Could not reach Stripe API: {ex.Message}");
        }
    }

    public Task<GatewayWebhookVerificationResult> VerifyAndParseWebhookAsync(
        GatewayCredentials credentials,
        HttpRequest httpRequest,
        string rawBody,
        CancellationToken cancellationToken)
    {
        var signatureHeader = httpRequest.Headers["Stripe-Signature"].FirstOrDefault();
        return Task.FromResult(VerifyAndParseWebhook(credentials.WebhookSecret, signatureHeader, rawBody, DateTimeOffset.UtcNow));
    }

    public static GatewayWebhookVerificationResult VerifyAndParseWebhook(
        string? configuredWebhookSecret,
        string? stripeSignatureHeader,
        string rawBody,
        DateTimeOffset nowUtc,
        TimeSpan? replayTolerance = null)
    {
        if (string.IsNullOrWhiteSpace(configuredWebhookSecret))
        {
            return Invalid("Workspace Stripe webhook signing secret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(stripeSignatureHeader))
        {
            return Invalid("Missing Stripe-Signature header.");
        }

        long? timestamp = null;
        var v1Signatures = new List<string>();

        foreach (var part in stripeSignatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0 || idx == part.Length - 1) continue;
            var key = part[..idx].Trim();
            var value = part[(idx + 1)..].Trim();

            if (string.Equals(key, "t", StringComparison.Ordinal) && long.TryParse(value, out var parsedTs))
            {
                timestamp = parsedTs;
            }
            else if (string.Equals(key, "v1", StringComparison.Ordinal))
            {
                v1Signatures.Add(value);
            }
        }

        if (timestamp is null || v1Signatures.Count == 0)
        {
            return Invalid("Malformed Stripe-Signature header.");
        }

        var tolerance = replayTolerance ?? DefaultReplayTolerance;
        var timestampDelta = Math.Abs(nowUtc.ToUnixTimeSeconds() - timestamp.Value);
        if (timestampDelta > (long)tolerance.TotalSeconds)
        {
            return Invalid("Stripe webhook timestamp is outside the allowed replay tolerance.");
        }

        var signedPayload = $"{timestamp.Value}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(configuredWebhookSecret.Trim()));
        var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));

        var matched = false;
        foreach (var candidateHex in v1Signatures)
        {
            if (TryDecodeHex(candidateHex, out var candidateBytes)
                && candidateBytes.Length == expectedHash.Length
                && CryptographicOperations.FixedTimeEquals(expectedHash, candidateBytes))
            {
                matched = true;
                break;
            }
        }

        if (!matched)
        {
            return Invalid("Invalid Stripe-Signature HMAC.");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var eventId = GetStringProperty(root, "id");
            var eventType = GetStringProperty(root, "type");

            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType))
            {
                return Invalid("Stripe event is missing id or type.");
            }

            if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("object", out var obj))
            {
                return Invalid("Stripe event is missing data.object.");
            }

            var sessionId = GetStringProperty(obj, "id");
            var clientReferenceId = GetStringProperty(obj, "client_reference_id");
            var paymentStatus = GetStringProperty(obj, "payment_status");
            var paymentMethod = ExtractPaymentMethodType(obj);
            var createdAt = TryGetUnixDateTimeOffset(root, "created") ?? nowUtc;

            PaymentCheckoutStatus? mappedStatus = eventType switch
            {
                "checkout.session.completed" when string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase) => PaymentCheckoutStatus.Paid,
                "checkout.session.async_payment_succeeded" => PaymentCheckoutStatus.Paid,
                "checkout.session.expired" => PaymentCheckoutStatus.Expired,
                "checkout.session.async_payment_failed" => PaymentCheckoutStatus.Failed,
                _ => null
            };

            return new GatewayWebhookVerificationResult(
                IsValid: true,
                ErrorMessage: null,
                ProviderEventId: eventId,
                EventType: eventType,
                ProviderCheckoutId: sessionId,
                ExternalReference: clientReferenceId,
                MappedStatus: mappedStatus,
                ProviderPaymentMethod: paymentMethod,
                PaidAt: mappedStatus == PaymentCheckoutStatus.Paid ? createdAt : null,
                FailureReason: mappedStatus == PaymentCheckoutStatus.Failed ? "Stripe reported async payment failure." : null);
        }
        catch (JsonException ex)
        {
            return Invalid($"Invalid Stripe webhook JSON payload: {ex.Message}");
        }
    }

    private static void ApplyBearerAuth(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    private static bool TryDecodeHex(string hex, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromHexString(hex);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static string? ExtractPaymentMethodType(JsonElement obj)
    {
        if (obj.TryGetProperty("payment_method_types", out var types)
            && types.ValueKind == JsonValueKind.Array
            && types.GetArrayLength() > 0)
        {
            var first = types[0].GetString();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first.ToUpperInvariant();
            }
        }

        return null;
    }

    private static string? GetStringProperty(JsonElement root, string name)
        => root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static DateTimeOffset? TryGetUnixDateTimeOffset(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var element) && element.TryGetInt64(out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }

    private static string ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "Empty response";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
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
