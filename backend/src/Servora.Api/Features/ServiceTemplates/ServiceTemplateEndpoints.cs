using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.ServiceTemplates;

public static class ServiceTemplateEndpoints
{
    public sealed record ServiceTemplateDto(int Id, string Name, string? Description, bool IsActive, IReadOnlyCollection<ChecklistItemDto> ChecklistItems);
    public sealed record ServiceTemplatePageDto(IReadOnlyCollection<ServiceTemplateDto> Items, int TotalCount, int Page, int PageSize);
    public sealed record ChecklistItemDto(int Id, string Title, string? Description, int SortOrder);
    public sealed record UpsertServiceTemplateRequest(string Name, string? Description, bool IsActive, IReadOnlyCollection<UpsertChecklistItemRequest> ChecklistItems);
    public sealed record UpsertChecklistItemRequest(int? Id, string Title, string? Description, int SortOrder);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/service-templates", ListAsync)
            .WithName("GetServiceTemplates")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapGet("/service-templates/{id:int}", GetAsync)
            .WithName("GetServiceTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPost("/service-templates", CreateAsync)
            .WithName("CreateServiceTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/service-templates/{id:int}", UpdateAsync)
            .WithName("UpdateServiceTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapDelete("/service-templates/{id:int}", DeleteAsync)
            .WithName("DeleteServiceTemplate")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);
    }

    private static async Task<IResult> ListAsync(AppDbContext context, IShopContext shopContext, int? page, int? pageSize, string? search, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var query = context.Set<ServiceTemplate>()
            .AsNoTracking()
            .Include(template => template.ChecklistItems)
            .Where(template => template.ShopId == shopId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(template => template.Name.Contains(term) || (template.Description != null && template.Description.Contains(term)));
        }

        var shouldPage = page.HasValue || pageSize.HasValue || !string.IsNullOrWhiteSpace(search);
        var currentPage = Math.Max(1, page ?? 1);
        var currentPageSize = Math.Clamp(pageSize ?? 10, 1, 100);
        var totalCount = shouldPage ? await query.CountAsync(cancellationToken) : 0;
        query = query.OrderBy(template => template.Name);
        if (shouldPage) query = query.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize);

        var templates = await query
            .ToListAsync(cancellationToken);
        var items = templates.Select(ToDto).ToList();
        return shouldPage ? Results.Ok(new ServiceTemplatePageDto(items, totalCount, currentPage, currentPageSize)) : Results.Ok(items);
    }

    private static async Task<IResult> GetAsync(int id, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var template = await context.Set<ServiceTemplate>()
            .AsNoTracking()
            .Include(item => item.ChecklistItems)
            .Where(item => item.Id == id && item.ShopId == shopId)
            .FirstOrDefaultAsync(cancellationToken);
        return template is null ? Results.NotFound() : Results.Ok(ToDto(template));
    }

    private static async Task<IResult> CreateAsync([FromBody] UpsertServiceTemplateRequest request, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (await context.Set<ServiceTemplate>().AnyAsync(item => item.ShopId == shopId && item.Name == request.Name.Trim(), cancellationToken))
        {
            return Results.BadRequest("Template name already exists.");
        }

        var template = new ServiceTemplate { ShopId = shopId };
        Apply(template, request, shopId);
        context.Set<ServiceTemplate>().Add(template);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/service-templates/{template.Id}", new { template.Id });
    }

    private static async Task<IResult> UpdateAsync(int id, [FromBody] UpsertServiceTemplateRequest request, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var template = await context.Set<ServiceTemplate>()
            .Include(item => item.ChecklistItems)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (template is null) return Results.NotFound();
        if (await context.Set<ServiceTemplate>().AnyAsync(item => item.ShopId == shopId && item.Id != id && item.Name == request.Name.Trim(), cancellationToken))
        {
            return Results.BadRequest("Template name already exists.");
        }

        Apply(template, request, shopId);
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(int id, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var template = await context.Set<ServiceTemplate>().FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (template is null) return Results.NotFound();
        template.IsActive = false;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static void Apply(ServiceTemplate template, UpsertServiceTemplateRequest request, int shopId)
    {
        template.Name = request.Name.Trim();
        template.Description = TrimOrNull(request.Description);
        template.IsActive = request.IsActive;

        template.ChecklistItems.Clear();
        foreach (var item in request.ChecklistItems.OrderBy(item => item.SortOrder))
        {
            template.ChecklistItems.Add(new ServiceTemplateChecklistItem
            {
                ShopId = shopId,
                Title = item.Title.Trim(),
                Description = TrimOrNull(item.Description),
                SortOrder = item.SortOrder
            });
        }
    }

    private static IResult? Validate(UpsertServiceTemplateRequest request)
    {
        var errors = new List<(string Field, string Message)>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors.Add(("name", "Template name is required."));
        if (request.Name?.Length > 120) errors.Add(("name", "Template name is too long."));
        var items = request.ChecklistItems.Where(item => !string.IsNullOrWhiteSpace(item.Title)).ToList();
        if (items.Count == 0) errors.Add(("checklistItems", "At least one checklist item is required."));
        if (items.Any(item => item.Title.Length > 200)) errors.Add(("checklistItems", "Checklist item title is too long."));
        return errors.Count == 0 ? null : Results.ValidationProblem(errors.GroupBy(error => error.Field).ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()));
    }

    private static ServiceTemplateDto ToDto(ServiceTemplate template)
        => new(
            template.Id,
            template.Name,
            template.Description,
            template.IsActive,
            template.ChecklistItems
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select(item => new ChecklistItemDto(item.Id, item.Title, item.Description, item.SortOrder))
                .ToList());

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
