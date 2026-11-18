namespace Servora.Api.Infrastructure.Notifications;

public interface INotificationService
{
    Task SendAsync(Guid userId, string message, CancellationToken cancellationToken = default);
    Task SendToRoleAsync(string roleName, string message, CancellationToken cancellationToken = default);
}
