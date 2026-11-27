using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Servora.Api.Features.Settings;

public static class GetShopProfileSettings
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/settings/shop-profile", async (AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken) =>
        {
            var settings = await ReadSettingsAsync(context, shopContext, cancellationToken);
            return Results.Ok(settings);
        })
        .WithName("GetShopProfileSettings")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.SettingsRead);
    }

    public static async Task<ShopProfileSettings> ReadSettingsAsync(
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var logoUrl = await context.Shops
            .AsNoTracking()
            .Where(shop => shop.Id == shopId)
            .Select(shop => shop.LogoUrl)
            .FirstOrDefaultAsync(cancellationToken);
        var storedSettings = (await context.AppSettings
            .AsNoTracking()
            .Where(setting => setting.ShopId == shopId && ShopProfileSettingKeys.Defaults.Keys.Contains(setting.Key))
            .ToListAsync(cancellationToken))
            .GroupBy(setting => setting.Key)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(setting => setting.UpdatedAt ?? setting.CreatedAt).First());
        var updatedAt = storedSettings.Values
            .Select(setting => (DateTimeOffset?)(setting.UpdatedAt ?? setting.CreatedAt))
            .Max();

        string Value(string key) => storedSettings.TryGetValue(key, out var setting)
            ? setting.Value
            : ShopProfileSettingKeys.Defaults[key];

        int IntValue(string key) => int.TryParse(Value(key), out var value)
            ? value
            : int.Parse(ShopProfileSettingKeys.Defaults[key]);
        decimal DecimalValue(string key) => decimal.TryParse(Value(key), CultureInfo.InvariantCulture, out var value)
            ? value
            : decimal.Parse(ShopProfileSettingKeys.Defaults[key], CultureInfo.InvariantCulture);
        bool BoolValue(string key) => bool.TryParse(Value(key), out var value)
            ? value
            : bool.Parse(ShopProfileSettingKeys.Defaults[key]);

        var rawTaxMode = Value(ShopProfileSettingKeys.InventoryDefaultTaxMode);
        var normalizedTaxMode = string.Equals(rawTaxMode, "Multiplier", StringComparison.OrdinalIgnoreCase)
            ? "Multiplier"
            : "Percentage";

        return new ShopProfileSettings(
            Value(ShopProfileSettingKeys.ShopName),
            Value(ShopProfileSettingKeys.PhoneNumber),
            Value(ShopProfileSettingKeys.WhatsAppNumber),
            Value(ShopProfileSettingKeys.Email),
            Value(ShopProfileSettingKeys.Address),
            Value(ShopProfileSettingKeys.TicketPrefix),
            Value(ShopProfileSettingKeys.TicketDateFormat),
            IntValue(ShopProfileSettingKeys.TicketStartNumber),
            IntValue(ShopProfileSettingKeys.TicketMinDigits),
            Value(ShopProfileSettingKeys.CurrencyCode),
            Value(ShopProfileSettingKeys.ReceiptFooter),
            DecimalValue(ShopProfileSettingKeys.InventoryDefaultTaxPercent),
            DecimalValue(ShopProfileSettingKeys.InventoryRoundingIncrement),
            Value(ShopProfileSettingKeys.InventoryDefaultUnit),
            BoolValue(ShopProfileSettingKeys.InventoryAllowDecimalQuantity),
            BoolValue(ShopProfileSettingKeys.InventoryAutoCreateCategoryOnImport),
            BoolValue(ShopProfileSettingKeys.RequirePhotoEvidenceOnIntake),
            BoolValue(ShopProfileSettingKeys.ShowPhotoEvidenceOnPublicTracking),
            BoolValue(ShopProfileSettingKeys.RequireApprovalEstimate),
            BoolValue(ShopProfileSettingKeys.AllowUnpaidPickupOverride),
            Value(ShopProfileSettingKeys.OperationalOpenTime),
            Value(ShopProfileSettingKeys.OperationalCloseTime),
            IntValue(ShopProfileSettingKeys.OperationalRequiredWorkingDaysPerWeek),
            IntValue(ShopProfileSettingKeys.OperationalRequiredWorkingDaysPerMonth),
            IntValue(ShopProfileSettingKeys.OperationalDefaultBreakMinutes),
            Value(ShopProfileSettingKeys.OperationalTimeZone),
            logoUrl,
            updatedAt,
            normalizedTaxMode);
    }
}
