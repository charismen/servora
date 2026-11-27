using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Payroll;

public static class CompensationTemplateEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public sealed record TemplateBonusConfigDto(
        string BonusTypeCode,
        bool IsActive,
        JsonElement Parameters);

    public sealed record CompensationTemplateDto(
        int Id,
        string Name,
        string? Description,
        string BasePayMode,
        decimal BaseHourlyRate,
        IReadOnlyCollection<WageTierBracket> TieredRates,
        FreelancePayPolicy PayPolicy,
        IReadOnlyCollection<TemplateBonusConfigDto> BonusConfigs,
        bool IsActive,
        int SortOrder,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record UpsertCompensationTemplateRequest(
        string Name,
        string? Description = null,
        string BasePayMode = "TieredDaily",
        decimal BaseHourlyRate = 0m,
        IReadOnlyCollection<WageTierBracket>? TieredRates = null,
        FreelancePayPolicy? PayPolicy = null,
        IReadOnlyCollection<TemplateBonusConfigDto>? BonusConfigs = null,
        bool IsActive = true,
        int SortOrder = 0);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/compensation-templates", ListTemplatesAsync)
            .WithName("GetCompensationTemplates")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPost("/compensation-templates", CreateTemplateAsync)
            .WithName("CreateCompensationTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/compensation-templates/{id:int}", UpdateTemplateAsync)
            .WithName("UpdateCompensationTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapDelete("/compensation-templates/{id:int}", DeleteTemplateAsync)
            .WithName("DeleteCompensationTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/compensation-templates/{id:int}/apply/{userId:int}", ApplyTemplateToWorkerAsync)
            .WithName("ApplyCompensationTemplateToWorker")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);
    }

    private static async Task<IResult> ListTemplatesAsync(
        bool? includeInactive,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var query = context.CompensationTemplates
            .AsNoTracking()
            .Where(item => item.ShopId == shopId);

        if (includeInactive != true)
        {
            query = query.Where(item => item.IsActive);
        }

        var items = await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(items.Select(ToDto).ToList());
    }

    private static async Task<IResult> CreateTemplateAsync(
        [FromBody] UpsertCompensationTemplateRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request, out var basePayMode);
        if (validation is not null) return validation;

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var name = request.Name.Trim();
        var duplicate = await context.CompensationTemplates
            .AnyAsync(item => item.ShopId == shopId && item.Name == name, cancellationToken);
        if (duplicate)
        {
            return Results.BadRequest($"A compensation template named '{name}' already exists.");
        }

        var brackets = NormalizeBrackets(request.TieredRates);
        var entity = new CompensationTemplate
        {
            ShopId = shopId,
            Name = name,
            Description = TrimOrNull(request.Description),
            BasePayMode = basePayMode,
            BaseHourlyRate = Math.Round(request.BaseHourlyRate, 2, MidpointRounding.AwayFromZero),
            TieredRatesJson = JsonSerializer.Serialize(brackets, JsonOptions),
            PayPolicyJson = JsonSerializer.Serialize(request.PayPolicy ?? new FreelancePayPolicy(), JsonOptions),
            BonusConfigsJson = JsonSerializer.Serialize(request.BonusConfigs ?? [], JsonOptions),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };

        context.CompensationTemplates.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "CompensationTemplate",
            entity.Id,
            "CompensationTemplateCreated",
            $"Created compensation template '{entity.Name}'.",
            cancellationToken: cancellationToken);

        return Results.Created($"/api/v1/compensation-templates/{entity.Id}", ToDto(entity));
    }

    private static async Task<IResult> UpdateTemplateAsync(
        int id,
        [FromBody] UpsertCompensationTemplateRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request, out var basePayMode);
        if (validation is not null) return validation;

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.CompensationTemplates
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (entity is null) return Results.NotFound();

        var name = request.Name.Trim();
        var duplicate = await context.CompensationTemplates
            .AnyAsync(item => item.ShopId == shopId && item.Id != id && item.Name == name, cancellationToken);
        if (duplicate)
        {
            return Results.BadRequest($"Another compensation template named '{name}' already exists.");
        }

        var brackets = NormalizeBrackets(request.TieredRates);
        entity.Name = name;
        entity.Description = TrimOrNull(request.Description);
        entity.BasePayMode = basePayMode;
        entity.BaseHourlyRate = Math.Round(request.BaseHourlyRate, 2, MidpointRounding.AwayFromZero);
        entity.TieredRatesJson = JsonSerializer.Serialize(brackets, JsonOptions);
        entity.PayPolicyJson = JsonSerializer.Serialize(request.PayPolicy ?? new FreelancePayPolicy(), JsonOptions);
        entity.BonusConfigsJson = JsonSerializer.Serialize(request.BonusConfigs ?? [], JsonOptions);
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;

        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "CompensationTemplate",
            entity.Id,
            "CompensationTemplateUpdated",
            $"Updated compensation template '{entity.Name}'.",
            cancellationToken: cancellationToken);

        return Results.Ok(ToDto(entity));
    }

    private static async Task<IResult> DeleteTemplateAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.CompensationTemplates
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (entity is null) return Results.NotFound();

        context.CompensationTemplates.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "CompensationTemplate",
            id,
            "CompensationTemplateDeleted",
            $"Deleted compensation template '{entity.Name}'.",
            cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> ApplyTemplateToWorkerAsync(
        int id,
        int userId,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var template = await context.CompensationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (template is null) return Results.NotFound();

        var shopUser = await context.ShopUsers
            .Include(item => item.FreelanceProfile)
                .ThenInclude(profile => profile!.BonusConfigs)
            .FirstOrDefaultAsync(item => item.ShopId == shopId && item.UserId == userId, cancellationToken);
        if (shopUser is null) return Results.NotFound();

        var profile = shopUser.FreelanceProfile;
        if (profile is null)
        {
            profile = new FreelanceProfile
            {
                ShopId = shopId,
                ShopUserId = shopUser.Id,
                CompensationTemplateId = template.Id,
                BasePayMode = template.BasePayMode,
                BaseHourlyRate = template.BaseHourlyRate,
                TieredRatesJson = template.TieredRatesJson,
                PayPolicyJson = template.PayPolicyJson
            };
            context.FreelanceProfiles.Add(profile);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            profile.CompensationTemplateId = template.Id;
            profile.BasePayMode = template.BasePayMode;
            profile.BaseHourlyRate = template.BaseHourlyRate;
            profile.TieredRatesJson = template.TieredRatesJson;
            profile.PayPolicyJson = template.PayPolicyJson;
        }

        var bonusTypesByCode = await context.BonusTypes
            .ToDictionaryAsync(item => item.Code, item => item, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var templateBonuses = ParseBonusConfigs(template.BonusConfigsJson);
        foreach (var item in templateBonuses)
        {
            if (!bonusTypesByCode.TryGetValue(item.BonusTypeCode, out var bonusType))
            {
                continue;
            }

            var rawJson = item.Parameters.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? "{}"
                : item.Parameters.GetRawText();

            var existingConfig = profile.BonusConfigs.FirstOrDefault(cfg => cfg.BonusTypeId == bonusType.Id);
            if (existingConfig is null)
            {
                context.WorkerBonusConfigs.Add(new WorkerBonusConfig
                {
                    ShopId = shopId,
                    FreelanceProfileId = profile.Id,
                    BonusTypeId = bonusType.Id,
                    ParametersJson = rawJson,
                    IsActive = item.IsActive
                });
            }
            else
            {
                existingConfig.ParametersJson = rawJson;
                existingConfig.IsActive = item.IsActive;
            }
        }

        shopUser.EmploymentType = EmploymentType.Freelance;
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "FreelanceProfile",
            userId,
            "CompensationTemplateApplied",
            $"Applied compensation template '{template.Name}' to user #{userId}.",
            cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    private static IResult? ValidateRequest(UpsertCompensationTemplateRequest request, out BasePayMode basePayMode)
    {
        basePayMode = BasePayMode.TieredDaily;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest("Template name is required.");
        }
        if (request.Name.Trim().Length > 120)
        {
            return Results.BadRequest("Template name must be 120 characters or fewer.");
        }
        if (request.BaseHourlyRate < 0m)
        {
            return Results.BadRequest("Base hourly rate cannot be negative.");
        }
        if (!Enum.TryParse<BasePayMode>(request.BasePayMode, true, out var parsed) || !Enum.IsDefined(parsed))
        {
            return Results.BadRequest("Invalid base pay mode.");
        }

        basePayMode = parsed;
        return null;
    }

    private static List<WageTierBracket> NormalizeBrackets(IReadOnlyCollection<WageTierBracket>? brackets) =>
        (brackets ?? [])
            .Where(bracket => bracket.MaxHours >= bracket.MinHours && bracket.MinHours >= 0)
            .OrderBy(bracket => bracket.MinHours)
            .ToList();

    private static List<TemplateBonusConfigDto> ParseBonusConfigs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<TemplateBonusConfigDto>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static CompensationTemplateDto ToDto(CompensationTemplate entity) => new(
        entity.Id,
        entity.Name,
        entity.Description,
        entity.BasePayMode.ToString(),
        entity.BaseHourlyRate,
        TieredWageCalculator.ParseBrackets(entity.TieredRatesJson),
        TieredWageCalculator.ParsePayPolicy(entity.PayPolicyJson),
        ParseBonusConfigs(entity.BonusConfigsJson),
        entity.IsActive,
        entity.SortOrder,
        entity.CreatedAt,
        entity.UpdatedAt);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
