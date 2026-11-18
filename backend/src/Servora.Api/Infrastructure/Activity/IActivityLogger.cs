namespace Servora.Api.Infrastructure.Activity;

public interface IActivityLogger
{
    Task LogAsync(
        string entityType,
        int entityId,
        string actionType,
        string title,
        string? description = null,
        string? metadataJson = null,
        CancellationToken cancellationToken = default);
}
