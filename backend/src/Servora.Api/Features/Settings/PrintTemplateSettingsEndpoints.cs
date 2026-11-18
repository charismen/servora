using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Settings;

public static class PrintTemplateSettingsEndpoints
{
    public sealed record Request(
        string TermsAndConditions,
        bool ShowPhysicalCondition,
        bool ShowAccessories,
        bool ShowPhotoNotes,
        bool ShowQrCode,
        bool ShowPhoneNumber,
        bool ShowPaymentSummary = true);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.TermsAndConditions).MaximumLength(1000);
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/settings/print-template", async (AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await ReadSettingsAsync(context, shopContext, cancellationToken));
        })
        .WithName("GetPrintTemplateSettings")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPut("/settings/print-template", async (
            [FromBody] Request request,
            IValidator<Request> validator,
            AppDbContext context,
            IShopContext shopContext,
            IActivityLogger activityLogger,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid) return Results.ValidationProblem(validationResult.ToDictionary());

            var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
            var values = new Dictionary<string, string>
            {
                [PrintTemplateSettingKeys.TermsAndConditions] = request.TermsAndConditions.Trim(),
                [PrintTemplateSettingKeys.ShowPhysicalCondition] = request.ShowPhysicalCondition.ToString().ToLowerInvariant(),
                [PrintTemplateSettingKeys.ShowAccessories] = request.ShowAccessories.ToString().ToLowerInvariant(),
                [PrintTemplateSettingKeys.ShowPhotoNotes] = request.ShowPhotoNotes.ToString().ToLowerInvariant(),
                [PrintTemplateSettingKeys.ShowQrCode] = request.ShowQrCode.ToString().ToLowerInvariant(),
                [PrintTemplateSettingKeys.ShowPhoneNumber] = request.ShowPhoneNumber.ToString().ToLowerInvariant(),
                [PrintTemplateSettingKeys.ShowPaymentSummary] = request.ShowPaymentSummary.ToString().ToLowerInvariant()
            };

            var existingSettings = await context.AppSettings
                .Where(setting => setting.ShopId == shopId && values.Keys.Contains(setting.Key))
                .ToDictionaryAsync(setting => setting.Key, cancellationToken);

            foreach (var (key, value) in values)
            {
                if (existingSettings.TryGetValue(key, out var setting))
                {
                    setting.Value = value;
                    setting.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    context.AppSettings.Add(new AppSetting { ShopId = shopId, Key = key, Value = value });
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            await activityLogger.LogAsync(
                "Settings",
                shopId,
                "PrintTemplateUpdated",
                "Print template settings updated",
                metadataJson: JsonSerializer.Serialize(new { keys = values.Keys }),
                cancellationToken: cancellationToken);

            return Results.Ok(await ReadSettingsAsync(context, shopContext, cancellationToken));
        })
        .WithName("UpdatePrintTemplateSettings")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.SettingsWrite);
    }

    public static async Task<PrintTemplateSettings> ReadSettingsAsync(
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var settings = await context.AppSettings
            .AsNoTracking()
            .Where(setting => setting.ShopId == shopId && PrintTemplateSettingKeys.Defaults.Keys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

        string Value(string key) => settings.GetValueOrDefault(key) ?? PrintTemplateSettingKeys.Defaults[key];
        bool BoolValue(string key) => bool.TryParse(Value(key), out var value) ? value : bool.Parse(PrintTemplateSettingKeys.Defaults[key]);

        return new PrintTemplateSettings(
            Value(PrintTemplateSettingKeys.TermsAndConditions),
            BoolValue(PrintTemplateSettingKeys.ShowPhysicalCondition),
            BoolValue(PrintTemplateSettingKeys.ShowAccessories),
            BoolValue(PrintTemplateSettingKeys.ShowPhotoNotes),
            BoolValue(PrintTemplateSettingKeys.ShowQrCode),
            BoolValue(PrintTemplateSettingKeys.ShowPhoneNumber),
            BoolValue(PrintTemplateSettingKeys.ShowPaymentSummary));
    }
}
