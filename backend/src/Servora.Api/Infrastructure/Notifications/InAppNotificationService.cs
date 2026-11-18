using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Servora.Api.Infrastructure.Notifications;

public class InAppNotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<InAppNotificationService> _logger;

    public InAppNotificationService(IHubContext<NotificationHub> hubContext, ILogger<InAppNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendAsync(Guid userId, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending in-app notification to User {UserId}: {Message}", userId, message);
        await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", message, cancellationToken);
    }

    public async Task SendToRoleAsync(string roleName, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending in-app notification to Role {RoleName}: {Message}", roleName, message);
        await _hubContext.Clients.Group(roleName).SendAsync("ReceiveNotification", message, cancellationToken);
    }
}
