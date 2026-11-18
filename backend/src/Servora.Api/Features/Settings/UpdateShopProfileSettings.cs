using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace Servora.Api.Features.Settings;

public static class UpdateShopProfileSettings
{
    private static readonly Dictionary<string, string> AllowedLogoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    public sealed record Request(
        string ShopName,
        string PhoneNumber,
        string WhatsAppNumber,
        string Email,
        string Address,
        string TicketPrefix,
        string TicketDateFormat,
        int TicketStartNumber,
        int TicketMinDigits,
        string CurrencyCode,
        string ReceiptFooter,
        decimal InventoryDefaultTaxPercent,
        decimal InventoryRoundingIncrement,
        string InventoryDefaultUnit,
        bool InventoryAllowDecimalQuantity,
        bool InventoryAutoCreateCategoryOnImport,
        bool RequirePhotoEvidenceOnIntake,
        bool ShowPhotoEvidenceOnPublicTracking,
        bool RequireApprovalEstimate,
        bool AllowUnpaidPickupOverride,
        string? OperationalOpenTime = null,
        string? OperationalCloseTime = null,
        int? OperationalRequiredWorkingDaysPerWeek = null,
        int? OperationalRequiredWorkingDaysPerMonth = null,
        int? OperationalDefaultBreakMinutes = null,
        string? OperationalTimeZone = null,
        DateTimeOffset? UpdatedAt = null);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.ShopName).NotEmpty().MaximumLength(160);
            RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(40);
            RuleFor(request => request.WhatsAppNumber).NotEmpty().MaximumLength(40);
            RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(160);
            RuleFor(request => request.Address).NotEmpty().MaximumLength(500);
            RuleFor(request => request.TicketPrefix).NotEmpty().MaximumLength(12).Matches("^[A-Za-z0-9-]+$");
            RuleFor(request => request.TicketDateFormat).NotEmpty().Must(format => format is "yyyyMMdd" or "yyyyMM" or "yyMMdd");
            RuleFor(request => request.TicketStartNumber).InclusiveBetween(1, 999999);
            RuleFor(request => request.TicketMinDigits).InclusiveBetween(1, 10);
            RuleFor(request => request.CurrencyCode)
                .Must(code => code is "IDR" or "USD" or "SGD" or "EUR" or "MYR" or "GBP" or "AUD")
                .WithMessage("Unsupported currency code.");
            RuleFor(request => request.ReceiptFooter).MaximumLength(500);
            RuleFor(request => request.InventoryDefaultTaxPercent).InclusiveBetween(0, 100);
            RuleFor(request => request.InventoryRoundingIncrement).InclusiveBetween(1, 1000000);
            RuleFor(request => request.InventoryDefaultUnit).NotEmpty().MaximumLength(20);
            RuleFor(request => request.OperationalOpenTime)
                .Must(value => string.IsNullOrWhiteSpace(value) || TimeOnly.TryParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                .WithMessage("Operational open time must be in HH:mm format.");
            RuleFor(request => request.OperationalCloseTime)
                .Must(value => string.IsNullOrWhiteSpace(value) || TimeOnly.TryParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                .WithMessage("Operational close time must be in HH:mm format.");
            RuleFor(request => request.OperationalRequiredWorkingDaysPerWeek)
                .InclusiveBetween(1, 7)
                .When(request => request.OperationalRequiredWorkingDaysPerWeek.HasValue);
            RuleFor(request => request.OperationalRequiredWorkingDaysPerMonth)
                .InclusiveBetween(1, 31)
                .When(request => request.OperationalRequiredWorkingDaysPerMonth.HasValue);
            RuleFor(request => request.OperationalDefaultBreakMinutes)
                .InclusiveBetween(0, 480)
                .When(request => request.OperationalDefaultBreakMinutes.HasValue);
            RuleFor(request => request.OperationalTimeZone)
                .MaximumLength(100)
                .When(request => !string.IsNullOrWhiteSpace(request.OperationalTimeZone));
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/settings/shop-profile", async (
            [FromBody] Request request,
            IValidator<Request> validator,
            AppDbContext context,
            IShopContext shopContext,
            IActivityLogger activityLogger,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
            var openTime = string.IsNullOrWhiteSpace(request.OperationalOpenTime)
                ? ShopProfileSettingKeys.Defaults[ShopProfileSettingKeys.OperationalOpenTime]
                : request.OperationalOpenTime.Trim();
            var closeTime = string.IsNullOrWhiteSpace(request.OperationalCloseTime)
                ? ShopProfileSettingKeys.Defaults[ShopProfileSettingKeys.OperationalCloseTime]
                : request.OperationalCloseTime.Trim();
            var daysPerWeek = request.OperationalRequiredWorkingDaysPerWeek ?? int.Parse(ShopProfileSettingKeys.Defaults[ShopProfileSettingKeys.OperationalRequiredWorkingDaysPerWeek]);
            var daysPerMonth = request.OperationalRequiredWorkingDaysPerMonth ?? int.Parse(ShopProfileSettingKeys.Defaults[ShopProfileSettingKeys.OperationalRequiredWorkingDaysPerMonth]);
            var defaultBreak = request.OperationalDefaultBreakMinutes ?? int.Parse(ShopProfileSettingKeys.Defaults[ShopProfileSettingKeys.OperationalDefaultBreakMinutes]);
            var timeZoneId = string.IsNullOrWhiteSpace(request.OperationalTimeZone)
                ? ShopProfileSettingKeys.Defaults[ShopProfileSettingKeys.OperationalTimeZone]
                : request.OperationalTimeZone.Trim();

            var values = new Dictionary<string, string>
            {
                [ShopProfileSettingKeys.ShopName] = request.ShopName.Trim(),
                [ShopProfileSettingKeys.PhoneNumber] = request.PhoneNumber.Trim(),
                [ShopProfileSettingKeys.WhatsAppNumber] = request.WhatsAppNumber.Trim(),
                [ShopProfileSettingKeys.Email] = request.Email.Trim(),
                [ShopProfileSettingKeys.Address] = request.Address.Trim(),
                [ShopProfileSettingKeys.TicketPrefix] = request.TicketPrefix.Trim().ToUpperInvariant(),
                [ShopProfileSettingKeys.TicketDateFormat] = request.TicketDateFormat,
                [ShopProfileSettingKeys.TicketStartNumber] = request.TicketStartNumber.ToString(),
                [ShopProfileSettingKeys.TicketMinDigits] = request.TicketMinDigits.ToString(),
                [ShopProfileSettingKeys.CurrencyCode] = request.CurrencyCode,
                [ShopProfileSettingKeys.ReceiptFooter] = request.ReceiptFooter.Trim(),
                [ShopProfileSettingKeys.InventoryDefaultTaxPercent] = request.InventoryDefaultTaxPercent.ToString(CultureInfo.InvariantCulture),
                [ShopProfileSettingKeys.InventoryRoundingIncrement] = request.InventoryRoundingIncrement.ToString(CultureInfo.InvariantCulture),
                [ShopProfileSettingKeys.InventoryDefaultUnit] = request.InventoryDefaultUnit.Trim(),
                [ShopProfileSettingKeys.InventoryAllowDecimalQuantity] = request.InventoryAllowDecimalQuantity.ToString(),
                [ShopProfileSettingKeys.InventoryAutoCreateCategoryOnImport] = request.InventoryAutoCreateCategoryOnImport.ToString(),
                [ShopProfileSettingKeys.RequirePhotoEvidenceOnIntake] = request.RequirePhotoEvidenceOnIntake.ToString(),
                [ShopProfileSettingKeys.ShowPhotoEvidenceOnPublicTracking] = request.ShowPhotoEvidenceOnPublicTracking.ToString(),
                [ShopProfileSettingKeys.RequireApprovalEstimate] = request.RequireApprovalEstimate.ToString(),
                [ShopProfileSettingKeys.AllowUnpaidPickupOverride] = request.AllowUnpaidPickupOverride.ToString(),
                [ShopProfileSettingKeys.OperationalOpenTime] = openTime,
                [ShopProfileSettingKeys.OperationalCloseTime] = closeTime,
                [ShopProfileSettingKeys.OperationalRequiredWorkingDaysPerWeek] = daysPerWeek.ToString(),
                [ShopProfileSettingKeys.OperationalRequiredWorkingDaysPerMonth] = daysPerMonth.ToString(),
                [ShopProfileSettingKeys.OperationalDefaultBreakMinutes] = defaultBreak.ToString(),
                [ShopProfileSettingKeys.OperationalTimeZone] = timeZoneId
            };

            var existingRows = await context.AppSettings
                .Where(setting => setting.ShopId == shopId && values.Keys.Contains(setting.Key))
                .ToListAsync(cancellationToken);
            var existingSettings = existingRows
                .GroupBy(setting => setting.Key)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(setting => setting.UpdatedAt ?? setting.CreatedAt).First());
            var currentUpdatedAt = existingSettings.Values
                .Select(setting => (DateTimeOffset?)(setting.UpdatedAt ?? setting.CreatedAt))
                .Max();
            if (request.UpdatedAt.HasValue && currentUpdatedAt.HasValue && request.UpdatedAt.Value != currentUpdatedAt.Value)
            {
                return Results.Problem(
                    detail: "Settings were updated by another user. Reload before saving again.",
                    statusCode: StatusCodes.Status409Conflict);
            }
            context.AppSettings.RemoveRange(existingRows.Except(existingSettings.Values));
            var changedKeys = values
                .Where(item => (existingSettings.TryGetValue(item.Key, out var setting) ? setting.Value : ShopProfileSettingKeys.Defaults[item.Key]) != item.Value)
                .Select(item => item.Key)
                .ToArray();

            foreach (var (key, value) in values)
            {
                if (existingSettings.TryGetValue(key, out var setting))
                {
                    setting.Value = value;
                    setting.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    context.AppSettings.Add(new AppSetting
                    {
                        ShopId = shopId,
                        Key = key,
                        Value = value
                    });
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            if (changedKeys.Length > 0)
            {
                await activityLogger.LogAsync(
                    "Settings",
                    shopId,
                    "SettingsUpdated",
                    "Settings updated",
                    string.Join(", ", changedKeys),
                    JsonSerializer.Serialize(new { changedKeys }),
                    cancellationToken);
            }

            return Results.Ok(await GetShopProfileSettings.ReadSettingsAsync(context, shopContext, cancellationToken));
        })
        .WithName("UpdateShopProfileSettings")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/settings/shop-profile/reset-defaults", async (
            AppDbContext context,
            IShopContext shopContext,
            IActivityLogger activityLogger,
            CancellationToken cancellationToken) =>
        {
            var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
            var keys = ShopProfileSettingKeys.Defaults.Keys.ToList();
            var existingSettings = await context.AppSettings
                .Where(setting => setting.ShopId == shopId && keys.Contains(setting.Key))
                .ToListAsync(cancellationToken);

            foreach (var setting in existingSettings)
            {
                setting.Value = ShopProfileSettingKeys.Defaults[setting.Key];
                setting.UpdatedAt = DateTimeOffset.UtcNow;
            }

            var existingKeys = existingSettings.Select(setting => setting.Key).ToHashSet();
            foreach (var (key, value) in ShopProfileSettingKeys.Defaults.Where(item => !existingKeys.Contains(item.Key)))
            {
                context.AppSettings.Add(new AppSetting { ShopId = shopId, Key = key, Value = value });
            }

            await context.SaveChangesAsync(cancellationToken);
            await activityLogger.LogAsync("Settings", shopId, "SettingsResetDefaults", "Settings reset to defaults", cancellationToken: cancellationToken);
            return Results.Ok(await GetShopProfileSettings.ReadSettingsAsync(context, shopContext, cancellationToken));
        })
        .WithName("ResetShopProfileSettings")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/settings/shop-profile/logo", async (
            IFormFile file,
            AppDbContext context,
            IShopContext shopContext,
            IActivityLogger activityLogger,
            CancellationToken cancellationToken) =>
        {
            if (file.Length == 0) return Results.BadRequest("Logo file is required.");
            if (file.Length > 5 * 1024 * 1024) return Results.BadRequest("Logo must be 5MB or smaller.");
            var extension = LogoExtension(file);
            if (extension is null) return Results.BadRequest("Only JPEG, PNG, or WEBP images are allowed.");

            var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
            var shop = await context.Shops.FirstOrDefaultAsync(item => item.Id == shopId, cancellationToken);
            if (shop is null) return Results.NotFound();

            var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "shop-logos");
            Directory.CreateDirectory(uploadRoot);
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var path = Path.Combine(uploadRoot, storedFileName);
            var oldLogoUrl = shop.LogoUrl;

            try
            {
                await using (var stream = File.Create(path))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                shop.LogoUrl = $"/uploads/shop-logos/{storedFileName}";
                await context.SaveChangesAsync(cancellationToken);

                if (oldLogoUrl?.StartsWith("/uploads/shop-logos/", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var oldPath = Path.Combine(uploadRoot, Path.GetFileName(oldLogoUrl));
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                }

                await activityLogger.LogAsync(
                    "Settings",
                    shopId,
                    "ShopLogoUpdated",
                    "Shop logo updated",
                    Path.GetFileName(file.FileName),
                    cancellationToken: cancellationToken);
            }
            catch
            {
                if (File.Exists(path)) File.Delete(path);
                throw;
            }

            return Results.Ok(await GetShopProfileSettings.ReadSettingsAsync(context, shopContext, cancellationToken));
        })
        .WithName("UpdateShopLogo")
        .Accepts<IFormFile>("multipart/form-data")
        .DisableAntiforgery()
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.SettingsWrite);
    }

    private static string? LogoExtension(IFormFile file)
    {
        if (AllowedLogoExtensions.TryGetValue(file.ContentType, out var extension)) return extension;
        return Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ".jpg",
            ".png" => ".png",
            ".webp" => ".webp",
            _ => null
        };
    }
}
