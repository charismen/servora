using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Activity;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Dispatch;

public sealed record DispatchConflictDto(
    int TicketId,
    string TicketNumber,
    string CustomerName,
    string DeviceSnapshot,
    DateTimeOffset ScheduledVisitStart,
    DateTimeOffset ScheduledVisitEnd,
    string ConflictType);

public sealed record ScheduledTicketSlot(
    int TicketId,
    string TicketNumber,
    string CustomerName,
    string DeviceSnapshot,
    int? AssignedTechnicianUserId,
    int? ServiceResourceId,
    DateTimeOffset Start,
    DateTimeOffset? End);

public sealed record ScheduleCandidate(
    int? ExcludeTicketId,
    int? AssignedTechnicianUserId,
    int? ServiceResourceId,
    DateTimeOffset Start,
    DateTimeOffset? End);

public static class DispatchConflictCalculator
{
    public static readonly TimeSpan DefaultVisitDuration = TimeSpan.FromMinutes(60);

    public static DateTimeOffset NormalizeEnd(DateTimeOffset start, DateTimeOffset? end)
        => end.HasValue && end.Value > start ? end.Value : start.Add(DefaultVisitDuration);

    public static bool IntervalsOverlap(
        DateTimeOffset startA,
        DateTimeOffset endA,
        DateTimeOffset startB,
        DateTimeOffset endB)
        => startA < endB && startB < endA;

    public static IReadOnlyList<DispatchConflictDto> EvaluateConflicts(
        ScheduleCandidate candidate,
        IEnumerable<ScheduledTicketSlot> existingSlots)
    {
        if (!candidate.AssignedTechnicianUserId.HasValue && !candidate.ServiceResourceId.HasValue)
        {
            return [];
        }

        var candidateEnd = NormalizeEnd(candidate.Start, candidate.End);
        var conflicts = new List<DispatchConflictDto>();

        foreach (var slot in existingSlots)
        {
            if (candidate.ExcludeTicketId.HasValue && slot.TicketId == candidate.ExcludeTicketId.Value)
            {
                continue;
            }

            var slotEnd = NormalizeEnd(slot.Start, slot.End);
            if (!IntervalsOverlap(candidate.Start, candidateEnd, slot.Start, slotEnd))
            {
                continue;
            }

            var techConflict = candidate.AssignedTechnicianUserId.HasValue
                && slot.AssignedTechnicianUserId.HasValue
                && candidate.AssignedTechnicianUserId.Value == slot.AssignedTechnicianUserId.Value;

            var resourceConflict = candidate.ServiceResourceId.HasValue
                && slot.ServiceResourceId.HasValue
                && candidate.ServiceResourceId.Value == slot.ServiceResourceId.Value;

            if (!techConflict && !resourceConflict)
            {
                continue;
            }

            var conflictType = techConflict && resourceConflict
                ? "Both"
                : techConflict
                    ? "Technician"
                    : "Resource";

            conflicts.Add(new DispatchConflictDto(
                slot.TicketId,
                slot.TicketNumber,
                slot.CustomerName,
                slot.DeviceSnapshot,
                slot.Start,
                slotEnd,
                conflictType));
        }

        return conflicts;
    }
}

public sealed record ScheduleTicketCommand(
    DateTimeOffset ScheduledVisitStart,
    DateTimeOffset? ScheduledVisitEnd,
    int? AssignedTechnicianUserId,
    int? ServiceResourceId,
    string? ServiceLocationType,
    string? SiteAddressSnapshot,
    string? DispatchNotes,
    bool AllowConflict,
    int? ActorUserId);

public sealed record DispatchOperationResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorMessage,
    int StatusCode = StatusCodes.Status200OK,
    IReadOnlyList<DispatchConflictDto>? Conflicts = null)
{
    public static DispatchOperationResult<T> Ok(T value, int statusCode = StatusCodes.Status200OK)
        => new(true, value, null, statusCode);

    public static DispatchOperationResult<T> Fail(string message, int statusCode = StatusCodes.Status400BadRequest)
        => new(false, default, message, statusCode);

    public static DispatchOperationResult<T> Conflict(string message, IReadOnlyList<DispatchConflictDto> conflicts)
        => new(false, default, message, StatusCodes.Status409Conflict, conflicts);
}

public sealed class DispatchSchedulingService
{
    private readonly AppDbContext _context;
    private readonly NotificationDispatcherService? _notificationDispatcher;
    private readonly IConfiguration? _configuration;

    public DispatchSchedulingService(
        AppDbContext context,
        NotificationDispatcherService? notificationDispatcher = null,
        IConfiguration? configuration = null)
    {
        _context = context;
        _notificationDispatcher = notificationDispatcher;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<DispatchConflictDto>> FindConflictsAsync(
        int shopId,
        ScheduleCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (!candidate.AssignedTechnicianUserId.HasValue && !candidate.ServiceResourceId.HasValue)
        {
            return [];
        }

        var candidateEnd = DispatchConflictCalculator.NormalizeEnd(candidate.Start, candidate.End);
        var windowStart = candidate.Start.AddHours(-24);

        var openScheduled = await _context.ServiceTickets.AsNoTracking()
            .Where(t => t.ShopId == shopId
                && t.DeletedAt == null
                && !t.Status.IsFinal
                && !t.Status.IsCancelled
                && t.ScheduledVisitStart != null
                && t.ScheduledVisitStart < candidateEnd
                && (t.ScheduledVisitEnd == null || t.ScheduledVisitEnd > candidate.Start)
                && t.ScheduledVisitStart >= windowStart)
            .Select(t => new ScheduledTicketSlot(
                t.Id,
                t.TicketNumber,
                t.CustomerNameSnapshot,
                t.DeviceSnapshot,
                t.AssignedTechnicianUserId,
                t.ServiceResourceId,
                t.ScheduledVisitStart!.Value,
                t.ScheduledVisitEnd))
            .ToListAsync(cancellationToken);

        return DispatchConflictCalculator.EvaluateConflicts(candidate, openScheduled);
    }

    public async Task<DispatchOperationResult<ServiceTicket>> ScheduleTicketAsync(
        int shopId,
        int ticketId,
        ScheduleTicketCommand command,
        CancellationToken cancellationToken)
    {
        var ticket = await _context.ServiceTickets
            .Include(t => t.Status)
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.ShopId == shopId && t.DeletedAt == null, cancellationToken);
        if (ticket is null)
        {
            return DispatchOperationResult<ServiceTicket>.Fail("Service ticket not found.", StatusCodes.Status404NotFound);
        }

        if (ticket.Status.IsCancelled)
        {
            return DispatchOperationResult<ServiceTicket>.Fail("Cancelled tickets cannot be scheduled.");
        }

        var start = command.ScheduledVisitStart;
        var end = command.ScheduledVisitEnd.HasValue
            ? command.ScheduledVisitEnd.Value
            : start.Add(DispatchConflictCalculator.DefaultVisitDuration);

        if (end <= start)
        {
            return DispatchOperationResult<ServiceTicket>.Fail("Scheduled end time must be after the start time.");
        }

        if ((end - start) > TimeSpan.FromDays(7))
        {
            return DispatchOperationResult<ServiceTicket>.Fail("Scheduled visit duration cannot exceed 7 days.");
        }

        if (command.AssignedTechnicianUserId.HasValue)
        {
            var techExists = await _context.ShopUsers.AsNoTracking()
                .AnyAsync(su => su.ShopId == shopId && su.UserId == command.AssignedTechnicianUserId.Value && su.Status == ShopUserStatus.Active, cancellationToken);
            if (!techExists)
            {
                return DispatchOperationResult<ServiceTicket>.Fail("Selected technician does not belong to this workspace.");
            }
        }

        if (command.ServiceResourceId.HasValue)
        {
            var resExists = await _context.ServiceResources.AsNoTracking()
                .AnyAsync(r => r.ShopId == shopId && r.Id == command.ServiceResourceId.Value && r.IsActive, cancellationToken);
            if (!resExists)
            {
                return DispatchOperationResult<ServiceTicket>.Fail("Selected service resource is unavailable in this workspace.");
            }
        }

        if (!string.IsNullOrWhiteSpace(command.ServiceLocationType))
        {
            if (!Enum.TryParse<ServiceLocationType>(command.ServiceLocationType, true, out var parsedLoc) || !Enum.IsDefined(parsedLoc))
            {
                return DispatchOperationResult<ServiceTicket>.Fail("Invalid service location type.");
            }
            ticket.ServiceLocationType = parsedLoc;
        }

        var conflicts = await FindConflictsAsync(
            shopId,
            new ScheduleCandidate(ticket.Id, command.AssignedTechnicianUserId, command.ServiceResourceId, start, end),
            cancellationToken);

        if (conflicts.Count > 0 && !command.AllowConflict)
        {
            return DispatchOperationResult<ServiceTicket>.Conflict(
                $"Scheduling conflict detected with {conflicts.Count} overlapping ticket(s). Confirm override to proceed.",
                conflicts);
        }

        ticket.AssignedTechnicianUserId = command.AssignedTechnicianUserId;
        ticket.ServiceResourceId = command.ServiceResourceId;
        ticket.ScheduledVisitStart = start;
        ticket.ScheduledVisitEnd = end;
        if (command.SiteAddressSnapshot is not null)
        {
            ticket.SiteAddressSnapshot = string.IsNullOrWhiteSpace(command.SiteAddressSnapshot)
                ? null
                : Truncate(command.SiteAddressSnapshot.Trim(), 500);
        }
        if (command.DispatchNotes is not null)
        {
            ticket.DispatchNotes = string.IsNullOrWhiteSpace(command.DispatchNotes)
                ? null
                : Truncate(command.DispatchNotes.Trim(), 1000);
        }
        if (ticket.DispatchStatus == DispatchStatus.Unscheduled)
        {
            ticket.DispatchStatus = DispatchStatus.Scheduled;
        }

        var conflictSuffix = conflicts.Count > 0 ? " (conflict override confirmed)" : string.Empty;
        _context.ServiceTicketHistories.Add(new ServiceTicketHistory
        {
            ShopId = shopId,
            ServiceTicketId = ticket.Id,
            ActorUserId = command.ActorUserId,
            EventType = TicketHistoryEventType.Updated,
            Note = $"Scheduled visit window: {start:yyyy-MM-dd HH:mm} - {end:HH:mm} UTC{conflictSuffix}.",
            IsPublicVisible = false,
            CreatedAt = DateTimeOffset.UtcNow
        });

        _context.ActivityLogs.Add(new ActivityLog
        {
            ShopId = shopId,
            ActorUserId = command.ActorUserId,
            EntityType = "ServiceTicket",
            EntityId = ticket.Id,
            ActionType = "TicketScheduled",
            Title = $"Scheduled {ticket.TicketNumber}",
            Description = $"Scheduled for {start:yyyy-MM-dd HH:mm} - {end:HH:mm} UTC{conflictSuffix}",
            MetadataJson = JsonSerializer.Serialize(new
            {
                start,
                end,
                technicianUserId = command.AssignedTechnicianUserId,
                serviceResourceId = command.ServiceResourceId,
                conflictOverride = conflicts.Count > 0
            })
        });

        await _context.SaveChangesAsync(cancellationToken);
        return DispatchOperationResult<ServiceTicket>.Ok(ticket);
    }

    public async Task<DispatchOperationResult<ServiceTicket>> UnscheduleTicketAsync(
        int shopId,
        int ticketId,
        int? actorUserId,
        CancellationToken cancellationToken)
    {
        var ticket = await _context.ServiceTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.ShopId == shopId && t.DeletedAt == null, cancellationToken);
        if (ticket is null)
        {
            return DispatchOperationResult<ServiceTicket>.Fail("Service ticket not found.", StatusCodes.Status404NotFound);
        }

        ticket.ScheduledVisitStart = null;
        ticket.ScheduledVisitEnd = null;
        ticket.ServiceResourceId = null;
        if (ticket.DispatchStatus != DispatchStatus.VisitCompleted)
        {
            ticket.DispatchStatus = DispatchStatus.Unscheduled;
        }

        _context.ServiceTicketHistories.Add(new ServiceTicketHistory
        {
            ShopId = shopId,
            ServiceTicketId = ticket.Id,
            ActorUserId = actorUserId,
            EventType = TicketHistoryEventType.Updated,
            Note = "Scheduled visit cleared and moved back to unscheduled backlog.",
            IsPublicVisible = false,
            CreatedAt = DateTimeOffset.UtcNow
        });

        _context.ActivityLogs.Add(new ActivityLog
        {
            ShopId = shopId,
            ActorUserId = actorUserId,
            EntityType = "ServiceTicket",
            EntityId = ticket.Id,
            ActionType = "TicketUnscheduled",
            Title = $"Unscheduled {ticket.TicketNumber}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return DispatchOperationResult<ServiceTicket>.Ok(ticket);
    }

    public async Task<DispatchOperationResult<ServiceTicket>> UpdateDispatchStatusAsync(
        int shopId,
        int ticketId,
        string targetStatusRaw,
        string? note,
        int? actorUserId,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DispatchStatus>(targetStatusRaw, true, out var targetStatus) || !Enum.IsDefined(targetStatus))
        {
            return DispatchOperationResult<ServiceTicket>.Fail("Invalid dispatch status.");
        }

        var ticket = await _context.ServiceTickets
            .Include(t => t.Status)
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.ShopId == shopId && t.DeletedAt == null, cancellationToken);
        if (ticket is null)
        {
            return DispatchOperationResult<ServiceTicket>.Fail("Service ticket not found.", StatusCodes.Status404NotFound);
        }

        if (ticket.Status.IsCancelled)
        {
            return DispatchOperationResult<ServiceTicket>.Fail("Cannot update dispatch status on a cancelled ticket.");
        }

        var now = DateTimeOffset.UtcNow;
        ticket.DispatchStatus = targetStatus;

        if (targetStatus == DispatchStatus.ArrivedOnSite && !ticket.VisitStartedAt.HasValue)
        {
            ticket.VisitStartedAt = now;
        }
        else if (targetStatus == DispatchStatus.VisitCompleted)
        {
            ticket.VisitStartedAt ??= now;
            ticket.VisitCompletedAt = now;
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            ticket.DispatchNotes = Truncate(note.Trim(), 1000);
        }

        var publicNote = targetStatus switch
        {
            DispatchStatus.EnRoute => "Technician is on the way to your location.",
            DispatchStatus.ArrivedOnSite => "Technician has arrived on-site and started the visit.",
            DispatchStatus.VisitCompleted => "Scheduled service visit has been completed.",
            DispatchStatus.Scheduled => "Service visit has been scheduled.",
            _ => "Dispatch status updated."
        };

        var fullNote = string.IsNullOrWhiteSpace(note) ? publicNote : $"{publicNote} ({note.Trim()})";
        var isPublic = targetStatus is DispatchStatus.EnRoute or DispatchStatus.ArrivedOnSite or DispatchStatus.VisitCompleted;

        _context.ServiceTicketHistories.Add(new ServiceTicketHistory
        {
            ShopId = shopId,
            ServiceTicketId = ticket.Id,
            ActorUserId = actorUserId,
            EventType = TicketHistoryEventType.Updated,
            Note = fullNote,
            IsPublicVisible = isPublic,
            CreatedAt = now
        });

        _context.ActivityLogs.Add(new ActivityLog
        {
            ShopId = shopId,
            ActorUserId = actorUserId,
            EntityType = "ServiceTicket",
            EntityId = ticket.Id,
            ActionType = "DispatchStatusUpdated",
            Title = $"Dispatch status updated for {ticket.TicketNumber}: {targetStatus}",
            Description = fullNote
        });

        await _context.SaveChangesAsync(cancellationToken);

        if (_notificationDispatcher is not null && targetStatus is DispatchStatus.EnRoute or DispatchStatus.ArrivedOnSite)
        {
            var eventType = targetStatus == DispatchStatus.EnRoute
                ? NotificationEventType.DispatchEnRoute
                : NotificationEventType.DispatchArrived;
            var frontendBaseUrl = (_configuration?["PublicFrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
            var trackUrl = ticket.PublicTrackingEnabled
                ? $"{frontendBaseUrl}/track/{ticket.PublicTrackingToken}"
                : $"{frontendBaseUrl}/app/tickets/{ticket.Id}";

            await _notificationDispatcher.PublishEventAsync(new NotificationEventEnvelope(
                ShopId: shopId,
                EventType: eventType,
                Title: targetStatus == DispatchStatus.EnRoute
                    ? $"Technician en route for {ticket.TicketNumber}"
                    : $"Technician arrived on-site for {ticket.TicketNumber}",
                Message: fullNote,
                DeepLinkUrl: trackUrl,
                CustomerName: ticket.Customer?.FullName,
                CustomerEmail: ticket.Customer?.Email,
                CustomerPhone: ticket.Customer?.PhoneNumber,
                ReferenceEntityType: "ServiceTicket",
                ReferenceEntityId: ticket.Id,
                Data: new { ticketId = ticket.Id, ticketNumber = ticket.TicketNumber, dispatchStatus = targetStatus.ToString() }),
                cancellationToken);
        }

        return DispatchOperationResult<ServiceTicket>.Ok(ticket);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
