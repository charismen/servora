using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Settings;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Notifications;

public static class NotificationTemplateEndpoints
{
    public sealed record NotificationTemplateDto(int Id, string Key, string Channel, string Provider, string Locale, string Name, string Body, bool IsActive);
    public sealed record NotificationTemplatePageDto(IReadOnlyCollection<NotificationTemplateDto> Items, int Page, int PageSize, int TotalItems, int TotalPages);
    public sealed record UpsertNotificationTemplateRequest(string Key, string? Channel, string? Provider, string Locale, string Name, string Body, bool IsActive);
    public sealed record GenerateWhatsAppRequest(string TemplateKey, string? Locale, string? BaseUrl = null);
    public sealed record GeneratedWhatsAppDto(string TemplateKey, string Locale, string PhoneNumber, string Message, string WhatsAppUrl);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/notification-templates", ListTemplatesAsync)
            .WithName("GetNotificationTemplates")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPost("/notification-templates", CreateTemplateAsync)
            .WithName("CreateNotificationTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/notification-templates/{id:int}", UpdateTemplateAsync)
            .WithName("UpdateNotificationTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapDelete("/notification-templates/{id:int}", DeleteTemplateAsync)
            .WithName("DeleteNotificationTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/service-tickets/{id:int}/whatsapp-message", GenerateWhatsAppMessageAsync)
            .WithName("GenerateServiceTicketWhatsAppMessage")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);
    }

    private static async Task<IResult> ListTemplatesAsync(
        string? channel,
        string? locale,
        int? page,
        int? pageSize,
        string? sort,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var query = context.NotificationTemplates.AsNoTracking().Where(item => item.ShopId == shopId);

        if (!string.IsNullOrWhiteSpace(channel) && TryParseChannel(channel, out var parsedChannel))
        {
            query = query.Where(item => item.Channel == parsedChannel);
        }

        if (!string.IsNullOrWhiteSpace(locale))
        {
            var normalizedLocale = NormalizeLocale(locale);
            query = query.Where(item => item.Locale == normalizedLocale);
        }

        var shouldPage = page.HasValue || pageSize.HasValue || !string.IsNullOrWhiteSpace(sort);
        var currentPage = Math.Max(1, page ?? 1);
        var currentPageSize = Math.Clamp(pageSize ?? 10, 1, 100);
        var totalItems = shouldPage ? await query.CountAsync(cancellationToken) : 0;

        IQueryable<NotificationTemplate> orderedQuery = sort?.Trim().ToLowerInvariant() switch
        {
            "key-desc" => query.OrderByDescending(item => item.Key).ThenBy(item => item.Locale),
            "locale-asc" => query.OrderBy(item => item.Locale).ThenBy(item => item.Key),
            "channel-asc" => query.OrderBy(item => item.Channel).ThenBy(item => item.Key).ThenBy(item => item.Locale),
            _ => query.OrderBy(item => item.Key).ThenBy(item => item.Locale).ThenBy(item => item.Channel)
        };

        if (shouldPage) orderedQuery = orderedQuery.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize);

        var templates = await orderedQuery
            .Select(item => new NotificationTemplateDto(
                item.Id,
                item.Key,
                item.Channel.ToString(),
                item.Provider,
                item.Locale,
                item.Name,
                item.Body,
                item.IsActive))
            .ToListAsync(cancellationToken);

        return shouldPage
            ? Results.Ok(new NotificationTemplatePageDto(templates, currentPage, currentPageSize, totalItems, (int)Math.Ceiling(totalItems / (double)currentPageSize)))
            : Results.Ok(templates);
    }

    private static async Task<IResult> CreateTemplateAsync(
        [FromBody] UpsertNotificationTemplateRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateTemplate(request, out var channel);
        if (validation is not null) return validation;

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var key = NormalizeKey(request.Key);
        var locale = NormalizeLocale(request.Locale);

        var exists = await context.NotificationTemplates.AnyAsync(item =>
            item.ShopId == shopId &&
            item.Channel == channel &&
            item.Key == key &&
            item.Locale == locale,
            cancellationToken);
        if (exists) return Results.BadRequest("Template key, channel, and locale already exist.");

        var template = new NotificationTemplate
        {
            ShopId = shopId,
            Key = key,
            Channel = channel,
            Provider = NormalizeProvider(request.Provider),
            Locale = locale,
            Name = request.Name.Trim(),
            Body = request.Body.Trim(),
            IsActive = request.IsActive
        };

        context.NotificationTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/notification-templates/{template.Id}", new { template.Id });
    }

    private static async Task<IResult> UpdateTemplateAsync(
        int id,
        [FromBody] UpsertNotificationTemplateRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateTemplate(request, out var channel);
        if (validation is not null) return validation;

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var template = await context.NotificationTemplates.FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (template is null) return Results.NotFound();

        var key = NormalizeKey(request.Key);
        var locale = NormalizeLocale(request.Locale);
        var duplicate = await context.NotificationTemplates.AnyAsync(item =>
            item.Id != id &&
            item.ShopId == shopId &&
            item.Channel == channel &&
            item.Key == key &&
            item.Locale == locale,
            cancellationToken);
        if (duplicate) return Results.BadRequest("Template key, channel, and locale already exist.");

        template.Key = key;
        template.Channel = channel;
        template.Provider = NormalizeProvider(request.Provider);
        template.Locale = locale;
        template.Name = request.Name.Trim();
        template.Body = request.Body.Trim();
        template.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteTemplateAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var template = await context.NotificationTemplates.FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (template is null) return Results.NotFound();

        context.NotificationTemplates.Remove(template);
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GenerateWhatsAppMessageAsync(
        int id,
        [FromBody] GenerateWhatsAppRequest request,
        HttpContext httpContext,
        AppDbContext context,
        IShopContext shopContext,
        IConfiguration configuration,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var locale = NormalizeLocale(request.Locale);
        var templateKey = string.IsNullOrWhiteSpace(request.TemplateKey) ? "ticket_created" : NormalizeKey(request.TemplateKey);

        var ticket = await context.ServiceTickets
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
        if (ticket is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(ticket.Customer.PhoneNumber)) return Results.BadRequest("Customer phone number is required.");

        var template = await context.NotificationTemplates.AsNoTracking()
            .Where(item => item.ShopId == shopId && item.Channel == NotificationChannel.WhatsApp && item.Key == templateKey && item.IsActive)
            .OrderByDescending(item => item.Locale == locale)
            .ThenByDescending(item => item.Locale == "en")
            .FirstOrDefaultAsync(cancellationToken);
        if (template is null) return Results.BadRequest("Notification template was not found.");

        var settings = await GetShopProfileSettings.ReadSettingsAsync(context, shopContext, cancellationToken);
        var approvals = await context.ApprovalRequests.AsNoTracking()
            .Where(item => item.ShopId == shopId && item.ServiceTicketId == id)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        var pendingApprovals = approvals.Where(item => item.Status == ApprovalStatus.Pending).ToList();
        var approvedApprovals = approvals.Where(item => item.Status == ApprovalStatus.Approved).ToList();
        var latestResolvedApproval = approvals.FirstOrDefault(item => item.Status != ApprovalStatus.Pending);

        decimal? estimatedCost = pendingApprovals.Any(item => item.EstimatedAmount.HasValue)
            ? pendingApprovals.Sum(item => item.EstimatedAmount ?? 0m)
            : approvedApprovals.Any(item => item.EstimatedAmount.HasValue)
                ? approvedApprovals.Sum(item => item.EstimatedAmount ?? 0m)
                : ticket.TotalAmount;

        var approvalStatus = pendingApprovals.Count > 0
            ? ApprovalStatus.Pending.ToString()
            : latestResolvedApproval?.Status.ToString() ?? "None";

        decimal? finalCost = ticket.TotalAmount ?? (ticket.AmountPaid > 0m ? ticket.AmountPaid : null);
        decimal? remainingBalance = finalCost.HasValue
            ? Math.Max(0m, TicketBillingCalculator.RoundMoney(finalCost.Value - ticket.AmountPaid))
            : null;

        var publicBaseUrl = request.BaseUrl;
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            publicBaseUrl = configuration["PublicFrontendBaseUrl"];
        }
        if (string.IsNullOrWhiteSpace(publicBaseUrl) && httpContext.Request.Headers.TryGetValue("Origin", out var originHeader) && !string.IsNullOrWhiteSpace(originHeader))
        {
            publicBaseUrl = originHeader.ToString();
        }
        if (string.IsNullOrWhiteSpace(publicBaseUrl) && httpContext.Request.Headers.TryGetValue("Referer", out var refererHeader) && !string.IsNullOrWhiteSpace(refererHeader))
        {
            if (Uri.TryCreate(refererHeader.ToString(), UriKind.Absolute, out var refererUri))
            {
                publicBaseUrl = refererUri.GetLeftPart(UriPartial.Authority);
            }
        }
        if (string.IsNullOrWhiteSpace(publicBaseUrl) && httpContext.Request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost) && !string.IsNullOrWhiteSpace(forwardedHost))
        {
            var scheme = httpContext.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto) && !string.IsNullOrWhiteSpace(forwardedProto)
                ? forwardedProto.ToString()
                : httpContext.Request.Scheme;
            publicBaseUrl = $"{scheme}://{forwardedHost}";
        }
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            publicBaseUrl = "http://localhost:5173";
        }
        var replacements = new Dictionary<string, string>
        {
            ["customer_name"] = ticket.CustomerNameSnapshot,
            ["ticket_number"] = ticket.TicketNumber,
            ["device"] = ticket.DeviceSnapshot,
            ["status"] = ticket.Status.Name,
            ["estimated_cost"] = Money(estimatedCost, settings.CurrencyCode),
            ["final_cost"] = Money(finalCost, settings.CurrencyCode),
            ["amount_paid"] = Money(ticket.AmountPaid, settings.CurrencyCode),
            ["remaining_balance"] = Money(remainingBalance, settings.CurrencyCode),
            ["payment_status"] = ticket.PaymentStatus.ToString(),
            ["shop_name"] = settings.ShopName,
            ["shop_phone"] = settings.WhatsAppNumber,
            ["ticket_url"] = $"{publicBaseUrl.TrimEnd('/')}/track/{ticket.PublicTrackingToken}",
            ["approval_status"] = approvalStatus
        };

        var message = replacements.Aggregate(template.Body, (current, item) => current.Replace("{{" + item.Key + "}}", item.Value));
        var phone = NormalizePhone(ticket.Customer.PhoneNumber);
        var url = $"https://wa.me/{phone}?text={Uri.EscapeDataString(message)}";

        await activityLogger.LogAsync("ServiceTicket", ticket.Id, "WhatsAppMessageGenerated", $"WhatsApp message generated for {ticket.TicketNumber}", templateKey, cancellationToken: cancellationToken);

        return Results.Ok(new GeneratedWhatsAppDto(template.Key, template.Locale, ticket.Customer.PhoneNumber, message, url));
    }

    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.StartsWith("0", StringComparison.Ordinal) ? "62" + digits[1..] : digits;
    }

    private static IResult? ValidateTemplate(UpsertNotificationTemplateRequest request, out NotificationChannel channel)
    {
        channel = NotificationChannel.WhatsApp;
        if (string.IsNullOrWhiteSpace(request.Key)) return Results.BadRequest("Template key is required.");
        if (string.IsNullOrWhiteSpace(request.Locale)) return Results.BadRequest("Template locale is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Template name is required.");
        if (string.IsNullOrWhiteSpace(request.Body)) return Results.BadRequest("Template body is required.");
        if (!TryParseChannel(request.Channel, out channel)) return Results.BadRequest("Invalid notification channel.");
        return null;
    }

    private static bool TryParseChannel(string? value, out NotificationChannel channel)
    {
        return Enum.TryParse(value ?? NotificationChannel.WhatsApp.ToString(), true, out channel);
    }

    private static string NormalizeKey(string key)
    {
        return key.Trim().ToLowerInvariant();
    }

    private static string NormalizeLocale(string? locale)
    {
        return string.IsNullOrWhiteSpace(locale) ? "en" : locale.Trim().ToLowerInvariant();
    }

    private static string NormalizeProvider(string? provider)
    {
        return string.IsNullOrWhiteSpace(provider) ? "manual" : provider.Trim().ToLowerInvariant();
    }

    private static string Money(decimal? amount, string? currencyCode = "IDR")
    {
        if (!amount.HasValue) return "-";
        var isUsd = string.Equals(currencyCode, "USD", StringComparison.OrdinalIgnoreCase);
        var culture = CultureInfo.GetCultureInfo(isUsd ? "en-US" : "id-ID");
        return amount.Value.ToString(isUsd ? "C2" : "C0", culture);
    }
}
