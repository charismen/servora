using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Servora.Api.Features.Notifications;

public sealed record ChannelSendResult(
    bool Succeeded,
    int? HttpStatusCode = null,
    string? ErrorMessage = null);

public sealed record SmtpConfig(
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    string? Password,
    string FromEmail,
    string FromName);

public sealed record WhatsAppConfig(
    WhatsAppProviderType Provider,
    string? ApiUrl,
    string? PhoneNumberId,
    string? ApiToken,
    string? AuthHeaderName,
    string? CustomPayloadTemplate);

public sealed record TelegramConfig(
    string BotToken,
    string ChatId,
    int? ThreadId);

public sealed record WebhookDispatchRequest(
    string TargetUrl,
    string SigningSecret,
    string EventType,
    string DeliveryId,
    string RawJsonBody,
    IReadOnlyDictionary<string, string>? CustomHeaders = null);

public interface IEmailSender
{
    Task<ChannelSendResult> SendAsync(
        SmtpConfig config,
        string toEmail,
        string subject,
        string plainTextBody,
        string? htmlBody,
        CancellationToken cancellationToken);
}

public interface IWhatsAppSender
{
    Task<ChannelSendResult> SendAsync(
        WhatsAppConfig config,
        string toPhone,
        string message,
        string eventType,
        CancellationToken cancellationToken);
}

public interface ITelegramSender
{
    Task<ChannelSendResult> SendAsync(
        TelegramConfig config,
        string htmlMessage,
        CancellationToken cancellationToken);
}

public interface IWebhookSender
{
    Task<ChannelSendResult> SendAsync(
        WebhookDispatchRequest request,
        CancellationToken cancellationToken);
}

public sealed class MailKitEmailSender : IEmailSender
{
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(ILogger<MailKitEmailSender> logger)
    {
        _logger = logger;
    }

    public async Task<ChannelSendResult> SendAsync(
        SmtpConfig config,
        string toEmail,
        string subject,
        string plainTextBody,
        string? htmlBody,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(config.FromName, config.FromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail.Trim()));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                TextBody = plainTextBody,
                HtmlBody = string.IsNullOrWhiteSpace(htmlBody)
                    ? BuildDefaultHtmlEmail(config.FromName, subject, plainTextBody)
                    : htmlBody
            };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = 15_000;

            var socketOption = config.UseSsl
                ? (config.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;

            await client.ConnectAsync(config.Host, config.Port, socketOption, cancellationToken);
            if (!string.IsNullOrWhiteSpace(config.Username))
            {
                await client.AuthenticateAsync(config.Username, config.Password ?? string.Empty, cancellationToken);
            }
            await client.SendAsync(new MailKit.SendRequest(message), cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            return new ChannelSendResult(true, 250, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP email delivery to {Recipient} failed.", toEmail);
            return new ChannelSendResult(false, null, TruncateError(ex.Message));
        }
    }

    public static string BuildDefaultHtmlEmail(string brandName, string heading, string plainTextBody, string? ctaUrl = null, string? ctaLabel = null)
    {
        var encodedBrand = WebUtility.HtmlEncode(brandName);
        var encodedHeading = WebUtility.HtmlEncode(heading);
        var encodedBody = WebUtility.HtmlEncode(plainTextBody).Replace("\n", "<br/>");
        var ctaBlock = !string.IsNullOrWhiteSpace(ctaUrl) && !string.IsNullOrWhiteSpace(ctaLabel)
            ? $"<div style=\"margin-top:24px;\"><a href=\"{WebUtility.HtmlEncode(ctaUrl)}\" style=\"display:inline-block;background:#1c1815;color:#ffffff;text-decoration:none;font-weight:600;font-size:14px;padding:12px 22px;border-radius:8px;\">{WebUtility.HtmlEncode(ctaLabel)}</a></div>"
            : string.Empty;

        return $"""
        <!DOCTYPE html>
        <html>
        <body style="margin:0;padding:24px;background:#faf8f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#1c1815;">
          <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e6dfd5;border-radius:12px;overflow:hidden;">
            <div style="padding:20px 24px;border-bottom:1px solid #e6dfd5;background:#f4f0ea;">
              <div style="font-size:13px;font-weight:700;letter-spacing:0.06em;text-transform:uppercase;color:#b45039;">{encodedBrand}</div>
              <h1 style="margin:6px 0 0;font-size:20px;line-height:1.3;color:#1c1815;">{encodedHeading}</h1>
            </div>
            <div style="padding:24px;font-size:14px;line-height:1.65;color:#4d443a;">
              <div>{encodedBody}</div>
              {ctaBlock}
            </div>
            <div style="padding:14px 24px;border-top:1px solid #e6dfd5;background:#faf8f5;font-size:12px;color:#8c8072;">
              Sent by {encodedBrand} · Powered by Servora
            </div>
          </div>
        </body>
        </html>
        """;
    }

    private static string TruncateError(string msg) => msg.Length <= 900 ? msg : msg[..900];
}

public sealed record BuiltWhatsAppHttpRequest(
    string Url,
    string JsonBody,
    string? AuthHeaderName,
    string? AuthHeaderValue);

public sealed class HttpWhatsAppSender : IWhatsAppSender
{
    public const string HttpClientName = "NotificationsWhatsApp";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpWhatsAppSender> _logger;

    public HttpWhatsAppSender(IHttpClientFactory httpClientFactory, ILogger<HttpWhatsAppSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public static string NormalizeWhatsAppPhone(string phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length >= 9)
        {
            return "62" + digits[1..];
        }
        return digits;
    }

    public static BuiltWhatsAppHttpRequest BuildHttpRequest(WhatsAppConfig config, string toPhone, string message, string eventType)
    {
        var normalizedPhone = NormalizeWhatsAppPhone(toPhone);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            throw new InvalidOperationException("Recipient phone number is empty or invalid.");
        }

        switch (config.Provider)
        {
            case WhatsAppProviderType.MetaCloud:
            {
                if (string.IsNullOrWhiteSpace(config.PhoneNumberId) || string.IsNullOrWhiteSpace(config.ApiToken))
                {
                    throw new InvalidOperationException("Meta Cloud API requires Phone Number ID and Access Token.");
                }

                var baseUrl = string.IsNullOrWhiteSpace(config.ApiUrl)
                    ? "https://graph.facebook.com/v21.0"
                    : config.ApiUrl.TrimEnd('/');
                var url = $"{baseUrl}/{Uri.EscapeDataString(config.PhoneNumberId.Trim())}/messages";
                var payload = JsonSerializer.Serialize(new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = normalizedPhone,
                    type = "text",
                    text = new { preview_url = true, body = message }
                });
                return new BuiltWhatsAppHttpRequest(url, payload, "Authorization", $"Bearer {config.ApiToken.Trim()}");
            }

            case WhatsAppProviderType.Fonnte:
            {
                if (string.IsNullOrWhiteSpace(config.ApiToken))
                {
                    throw new InvalidOperationException("Fonnte requires an API Token.");
                }

                var url = string.IsNullOrWhiteSpace(config.ApiUrl)
                    ? "https://api.fonnte.com/send"
                    : config.ApiUrl.Trim();
                var payload = JsonSerializer.Serialize(new
                {
                    target = normalizedPhone,
                    message
                });
                return new BuiltWhatsAppHttpRequest(url, payload, "Authorization", config.ApiToken.Trim());
            }

            case WhatsAppProviderType.Waha:
            {
                if (string.IsNullOrWhiteSpace(config.ApiUrl))
                {
                    throw new InvalidOperationException("WAHA requires an API URL (e.g. https://waha.example.com/api/sendText).");
                }

                var url = config.ApiUrl.Trim().EndsWith("/api/sendText", StringComparison.OrdinalIgnoreCase)
                    ? config.ApiUrl.Trim()
                    : $"{config.ApiUrl.TrimEnd('/')}/api/sendText";
                var payload = JsonSerializer.Serialize(new
                {
                    session = "default",
                    chatId = $"{normalizedPhone}@c.us",
                    text = message
                });
                var headerName = string.IsNullOrWhiteSpace(config.AuthHeaderName) ? "X-Api-Key" : config.AuthHeaderName.Trim();
                return new BuiltWhatsAppHttpRequest(url, payload, string.IsNullOrWhiteSpace(config.ApiToken) ? null : headerName, config.ApiToken?.Trim());
            }

            case WhatsAppProviderType.CustomHttp:
            {
                if (string.IsNullOrWhiteSpace(config.ApiUrl))
                {
                    throw new InvalidOperationException("Custom HTTP WhatsApp gateway requires a Target API URL.");
                }

                var template = string.IsNullOrWhiteSpace(config.CustomPayloadTemplate)
                    ? "{\"phone\":\"{{phone}}\",\"message\":\"{{message}}\",\"event\":\"{{event}}\"}"
                    : config.CustomPayloadTemplate;

                var jsonBody = template
                    .Replace("{{phone}}", JsonEscape(normalizedPhone), StringComparison.Ordinal)
                    .Replace("{{message}}", JsonEscape(message), StringComparison.Ordinal)
                    .Replace("{{event}}", JsonEscape(eventType), StringComparison.Ordinal);

                var headerName = string.IsNullOrWhiteSpace(config.AuthHeaderName) ? "Authorization" : config.AuthHeaderName.Trim();
                return new BuiltWhatsAppHttpRequest(
                    config.ApiUrl.Trim(),
                    jsonBody,
                    string.IsNullOrWhiteSpace(config.ApiToken) ? null : headerName,
                    config.ApiToken?.Trim());
            }

            default:
                throw new InvalidOperationException("WhatsApp automated API provider is set to Manual.");
        }
    }

    public async Task<ChannelSendResult> SendAsync(
        WhatsAppConfig config,
        string toPhone,
        string message,
        string eventType,
        CancellationToken cancellationToken)
    {
        try
        {
            var built = BuildHttpRequest(config, toPhone, message, eventType);
            using var req = new HttpRequestMessage(HttpMethod.Post, built.Url)
            {
                Content = new StringContent(built.JsonBody, Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(built.AuthHeaderName) && !string.IsNullOrWhiteSpace(built.AuthHeaderValue))
            {
                req.Headers.TryAddWithoutValidation(built.AuthHeaderName, built.AuthHeaderValue);
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var resp = await client.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (resp.IsSuccessStatusCode)
            {
                return new ChannelSendResult(true, (int)resp.StatusCode, null);
            }

            var err = string.IsNullOrWhiteSpace(body) ? resp.ReasonPhrase ?? "WhatsApp API error" : body;
            return new ChannelSendResult(false, (int)resp.StatusCode, err.Length > 500 ? err[..500] : err);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp delivery failed via {Provider}.", config.Provider);
            return new ChannelSendResult(false, null, ex.Message.Length > 500 ? ex.Message[..500] : ex.Message);
        }
    }

    private static string JsonEscape(string value)
    {
        var serialized = JsonSerializer.Serialize(value);
        return serialized.Length >= 2 ? serialized[1..^1] : serialized;
    }
}

public sealed class HttpTelegramSender : ITelegramSender
{
    public const string HttpClientName = "NotificationsTelegram";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpTelegramSender> _logger;

    public HttpTelegramSender(IHttpClientFactory httpClientFactory, ILogger<HttpTelegramSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ChannelSendResult> SendAsync(
        TelegramConfig config,
        string htmlMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.BotToken) || string.IsNullOrWhiteSpace(config.ChatId))
        {
            return new ChannelSendResult(false, null, "Telegram Bot Token and Chat ID are required.");
        }

        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["chat_id"] = config.ChatId.Trim(),
                ["text"] = htmlMessage,
                ["parse_mode"] = "HTML",
                ["disable_web_page_preview"] = true
            };
            if (config.ThreadId.HasValue && config.ThreadId.Value > 0)
            {
                payload["message_thread_id"] = config.ThreadId.Value;
            }

            var url = $"https://api.telegram.org/bot{config.BotToken.Trim()}/sendMessage";
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var resp = await client.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (resp.IsSuccessStatusCode)
            {
                return new ChannelSendResult(true, (int)resp.StatusCode, null);
            }

            return new ChannelSendResult(false, (int)resp.StatusCode, body.Length > 500 ? body[..500] : body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram Bot API delivery failed.");
            return new ChannelSendResult(false, null, ex.Message.Length > 500 ? ex.Message[..500] : ex.Message);
        }
    }
}

public static class WebhookSignatureHelper
{
    public static string ComputeHeader(string signingSecret, long unixTimestamp, string rawJsonBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{unixTimestamp}.{rawJsonBody}"));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={unixTimestamp},v1={hex}";
    }

    public static bool VerifyHeader(string signingSecret, string headerValue, string rawJsonBody, DateTimeOffset nowUtc, TimeSpan? tolerance = null)
    {
        if (string.IsNullOrWhiteSpace(signingSecret) || string.IsNullOrWhiteSpace(headerValue)) return false;

        long? ts = null;
        string? sig = null;
        foreach (var part in headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0 || idx >= part.Length - 1) continue;
            var key = part[..idx];
            var val = part[(idx + 1)..];
            if (key == "t" && long.TryParse(val, out var parsed)) ts = parsed;
            if (key == "v1") sig = val;
        }

        if (ts is null || string.IsNullOrWhiteSpace(sig)) return false;
        var maxDelta = (long)(tolerance ?? TimeSpan.FromMinutes(5)).TotalSeconds;
        if (Math.Abs(nowUtc.ToUnixTimeSeconds() - ts.Value) > maxDelta) return false;

        var expectedHeader = ComputeHeader(signingSecret, ts.Value, rawJsonBody);
        var expectedHex = expectedHeader[(expectedHeader.IndexOf("v1=", StringComparison.Ordinal) + 3)..];
        var a = Encoding.UTF8.GetBytes(expectedHex);
        var b = Encoding.UTF8.GetBytes(sig.ToLowerInvariant());
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

public static class WebhookUrlValidator
{
    public static (bool IsValid, string? ErrorMessage) ValidateStaticUrl(string? url, bool isProduction, bool allowPrivateTargets)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return (false, "Webhook URL must be a valid absolute URL.");
        }

        if (isProduction && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Webhook URLs must use HTTPS in Production.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Webhook URL must use HTTP or HTTPS.");
        }

        if (!allowPrivateTargets)
        {
            var host = uri.Host.Trim().ToLowerInvariant();
            if (host is "localhost" or "127.0.0.1" or "::1" or "0.0.0.0" || host.EndsWith(".local", StringComparison.Ordinal))
            {
                return (false, "Private or loopback webhook hosts are not allowed.");
            }

            if (IPAddress.TryParse(host, out var ip) && IsPrivateOrLoopback(ip))
            {
                return (false, "Private or loopback IP addresses are not allowed for webhook targets.");
            }
        }

        return (true, null);
    }

    public static async Task<(bool IsValid, string? ErrorMessage)> ValidateWithDnsAsync(
        string? url,
        bool isProduction,
        bool allowPrivateTargets,
        CancellationToken cancellationToken)
    {
        var staticCheck = ValidateStaticUrl(url, isProduction, allowPrivateTargets);
        if (!staticCheck.IsValid || allowPrivateTargets)
        {
            return staticCheck;
        }

        var uri = new Uri(url!.Trim());
        if (IPAddress.TryParse(uri.Host, out _))
        {
            return (true, null);
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
            if (addresses.Any(IsPrivateOrLoopback))
            {
                return (false, "Webhook host resolved to a private or loopback IP address.");
            }
            return (true, null);
        }
        catch (SocketException)
        {
            return (false, "Webhook host could not be resolved via DNS.");
        }
    }

    public static bool IsPrivateOrLoopback(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork && bytes.Length == 4)
        {
            // 10.0.0.0/8, 127.0.0.0/8, 169.254.0.0/16 (cloud metadata!), 172.16.0.0/12, 192.168.0.0/16, 0.0.0.0/8
            return bytes[0] == 10
                || bytes[0] == 127
                || bytes[0] == 0
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && bytes.Length == 16)
        {
            // Link-local fe80::/10 or Unique Local fc00::/7
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || (bytes[0] & 0xFE) == 0xFC;
        }

        return false;
    }
}

public sealed class HttpWebhookSender : IWebhookSender
{
    public const string HttpClientName = "NotificationsWebhook";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HttpWebhookSender> _logger;

    public HttpWebhookSender(
        IHttpClientFactory httpClientFactory,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<HttpWebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ChannelSendResult> SendAsync(
        WebhookDispatchRequest request,
        CancellationToken cancellationToken)
    {
        var allowPrivate = _configuration.GetValue("Webhooks:AllowPrivateTargets", !_environment.IsProduction());
        var validation = await WebhookUrlValidator.ValidateWithDnsAsync(
            request.TargetUrl,
            _environment.IsProduction(),
            allowPrivate,
            cancellationToken);

        if (!validation.IsValid)
        {
            return new ChannelSendResult(false, null, validation.ErrorMessage);
        }

        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var signature = WebhookSignatureHelper.ComputeHeader(request.SigningSecret, timestamp, request.RawJsonBody);

            using var req = new HttpRequestMessage(HttpMethod.Post, request.TargetUrl)
            {
                Content = new StringContent(request.RawJsonBody, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("X-Servora-Event", request.EventType);
            req.Headers.TryAddWithoutValidation("X-Servora-Delivery-Id", request.DeliveryId);
            req.Headers.TryAddWithoutValidation("X-Servora-Signature", signature);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("Servora-Webhooks", "1.0"));

            if (request.CustomHeaders is not null)
            {
                foreach (var (key, val) in request.CustomHeaders)
                {
                    if (!string.IsNullOrWhiteSpace(key)
                        && !key.StartsWith("X-Servora-", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(key, "Host", StringComparison.OrdinalIgnoreCase))
                    {
                        req.Headers.TryAddWithoutValidation(key.Trim(), val);
                    }
                }
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var resp = await client.SendAsync(req, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                return new ChannelSendResult(true, (int)resp.StatusCode, null);
            }

            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            var err = string.IsNullOrWhiteSpace(body) ? resp.ReasonPhrase ?? "Webhook target rejected payload" : body;
            return new ChannelSendResult(false, (int)resp.StatusCode, err.Length > 500 ? err[..500] : err);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Outbound webhook delivery failed to {TargetUrl}.", request.TargetUrl);
            return new ChannelSendResult(false, null, ex.Message.Length > 500 ? ex.Message[..500] : ex.Message);
        }
    }
}
