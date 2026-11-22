using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.Notifications;

namespace Servora.Api.Features.Maintenance;

public sealed record PreventiveMaintenanceRunResult(
    int ScannedSchedules,
    int UpcomingNotified,
    int TicketsAutoCreated);

public sealed class PreventiveMaintenanceJob
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PreventiveMaintenanceJob> _logger;
    private readonly NotificationDispatcherService? _notificationDispatcher;
    private readonly IConfiguration? _configuration;

    public PreventiveMaintenanceJob(
        AppDbContext context,
        INotificationService notificationService,
        ILogger<PreventiveMaintenanceJob> logger,
        NotificationDispatcherService? notificationDispatcher = null,
        IConfiguration? configuration = null)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
        _notificationDispatcher = notificationDispatcher;
        _configuration = configuration;
    }

    public async Task<PreventiveMaintenanceRunResult> ProcessDueSchedulesAsync(
        CancellationToken cancellationToken = default,
        int? onlyWorkspaceId = null)
    {
        var now = DateTimeOffset.UtcNow;

        var query = _context.MaintenanceSchedules
            .Include(s => s.Shop)
            .Include(s => s.ServiceAsset)
                .ThenInclude(a => a.Customer)
            .Include(s => s.ServiceAsset)
                .ThenInclude(a => a.SiteLocation)
            .Include(s => s.ServiceTemplate)
                .ThenInclude(t => t!.ChecklistItems)
            .Include(s => s.LastGeneratedTicket)
                .ThenInclude(t => t!.Status)
            .Where(s => s.Status == MaintenanceScheduleStatus.Active
                && s.Shop.IsActive
                && s.ServiceAsset.DeletedAt == null);

        if (onlyWorkspaceId.HasValue)
        {
            query = query.Where(s => s.ShopId == onlyWorkspaceId.Value);
        }

        var schedules = await query.ToListAsync(cancellationToken);
        var notified = 0;
        var created = 0;

        foreach (var schedule in schedules)
        {
            var isCalendarDue = schedule.TriggerType is MaintenanceTriggerType.Calendar or MaintenanceTriggerType.Both
                && schedule.NextDueAt.HasValue
                && schedule.NextDueAt.Value <= now;

            var isCalendarUpcoming = schedule.TriggerType is MaintenanceTriggerType.Calendar or MaintenanceTriggerType.Both
                && schedule.NextDueAt.HasValue
                && schedule.NextDueAt.Value <= now.AddDays(Math.Max(1, schedule.LeadTimeDays));

            var isMeterDue = schedule.TriggerType is MaintenanceTriggerType.Meter or MaintenanceTriggerType.Both
                && schedule.NextDueMeter.HasValue
                && schedule.ServiceAsset.MeterReading.HasValue
                && schedule.ServiceAsset.MeterReading.Value >= schedule.NextDueMeter.Value;

            var isDueNow = isCalendarDue || isMeterDue;
            var isUpcoming = isCalendarUpcoming || isMeterDue;

            if (!isUpcoming) continue;

            // Emit reminder notification at most once per 24 hours
            if (!schedule.LastNotifiedAt.HasValue || schedule.LastNotifiedAt.Value < now.AddHours(-24))
            {
                schedule.LastNotifiedAt = now;
                notified++;

                var message = isDueNow
                    ? $"Preventive maintenance DUE: {schedule.Title} ({schedule.ServiceAsset.Brand} {schedule.ServiceAsset.Model})"
                    : $"Preventive maintenance upcoming: {schedule.Title} ({schedule.ServiceAsset.Brand} {schedule.ServiceAsset.Model})";

                await _notificationService.SendToRoleAsync("Owner/Admin", message, cancellationToken);
                await _notificationService.SendToRoleAsync("Technician", message, cancellationToken);

                _context.ActivityLogs.Add(new Features.Activity.ActivityLog
                {
                    ShopId = schedule.ShopId,
                    EntityType = "MaintenanceSchedule",
                    EntityId = schedule.Id,
                    ActionType = isDueNow ? "MaintenanceDue" : "MaintenanceUpcoming",
                    Title = schedule.Title,
                    Description = message
                });

                if (_notificationDispatcher is not null)
                {
                    var frontendBaseUrl = (_configuration?["PublicFrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
                    await _notificationDispatcher.PublishEventAsync(new NotificationEventEnvelope(
                        ShopId: schedule.ShopId,
                        EventType: NotificationEventType.MaintenanceDue,
                        Title: isDueNow ? $"Maintenance Due: {schedule.Title}" : $"Maintenance Upcoming: {schedule.Title}",
                        Message: message,
                        DeepLinkUrl: $"{frontendBaseUrl}/app/maintenance",
                        CustomerName: schedule.ServiceAsset.Customer?.FullName,
                        CustomerEmail: schedule.ServiceAsset.Customer?.Email,
                        CustomerPhone: schedule.ServiceAsset.Customer?.PhoneNumber,
                        ReferenceEntityType: "MaintenanceSchedule",
                        ReferenceEntityId: schedule.Id,
                        Data: new { scheduleId = schedule.Id, title = schedule.Title, isDueNow, asset = $"{schedule.ServiceAsset.Brand} {schedule.ServiceAsset.Model}".Trim() }),
                        cancellationToken);
                }
            }

            if (isDueNow && schedule.AutoCreateTicket)
            {
                var hasOpenGeneratedTicket = schedule.LastGeneratedTicket is not null
                    && schedule.LastGeneratedTicket.DeletedAt == null
                    && !schedule.LastGeneratedTicket.Status.IsFinal
                    && !schedule.LastGeneratedTicket.Status.IsCancelled;

                if (!hasOpenGeneratedTicket)
                {
                    var ticket = await CreateTicketFromScheduleAsync(schedule, now, cancellationToken);
                    if (ticket is not null)
                    {
                        schedule.LastGeneratedTicketId = ticket.Id;
                        AdvanceNextDue(schedule, now);
                        created++;
                    }
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Preventive maintenance scan complete: {Scanned} scanned, {Notified} notified, {Created} auto-created tickets.",
            schedules.Count, notified, created);

        return new PreventiveMaintenanceRunResult(schedules.Count, notified, created);
    }

    public async Task<ServiceTicket?> CreateTicketFromScheduleAsync(
        MaintenanceSchedule schedule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var initialStatus = await _context.ServiceStatuses
            .Where(s => s.ShopId == schedule.ShopId)
            .OrderByDescending(s => s.IsInitial)
            .ThenBy(s => s.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (initialStatus is null) return null;

        var isInternal = schedule.Shop.OperatingMode == OperatingMode.InternalMaintenance;
        var asset = schedule.ServiceAsset;
        var customer = asset.Customer;

        var prefix = await _context.AppSettings
            .Where(s => s.ShopId == schedule.ShopId && s.Key == Features.Settings.ShopProfileSettingKeys.TicketPrefix)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? "PM";

        var seq = await _context.ServiceTickets.CountAsync(t => t.ShopId == schedule.ShopId, cancellationToken) + 1;
        var ticketNumber = $"{prefix}-PM-{now:yyyyMMdd}-{seq:D4}";

        var ticket = new ServiceTicket
        {
            ShopId = schedule.ShopId,
            CustomerId = customer.Id,
            DeviceId = asset.Id,
            StatusId = initialStatus.Id,
            AssignedTechnicianUserId = schedule.AssignedTechnicianUserId,
            TicketNumber = ticketNumber,
            PublicTrackingToken = Guid.NewGuid().ToString("N")[..24],
            PublicTrackingEnabled = !isInternal,
            CustomerNameSnapshot = customer.FullName,
            CustomerPhoneSnapshot = customer.PhoneNumber,
            DeviceSnapshot = $"{asset.Brand} {asset.Model}" + (string.IsNullOrWhiteSpace(asset.SerialNumber) ? "" : $" ({asset.SerialNumber})"),
            Priority = ServiceTicketPriority.Normal,
            IsInternalWorkOrder = isInternal,
            DepartmentCode = customer.DepartmentCode,
            CostCenter = customer.CostCenter,
            ServiceLocationType = schedule.DefaultLocationType,
            SiteLocationId = asset.SiteLocationId,
            SiteAddressSnapshot = asset.SiteLocation?.Address ?? customer.Address,
            ScheduledVisitStart = schedule.NextDueAt ?? now,
            Complaint = $"[Preventive Maintenance] {schedule.Title}" + (string.IsNullOrWhiteSpace(schedule.Description) ? "" : $" — {schedule.Description}"),
            PaymentStatus = isInternal ? PaymentStatus.Waived : PaymentStatus.Unpaid
        };

        _context.ServiceTickets.Add(ticket);
        await _context.SaveChangesAsync(cancellationToken);

        if (schedule.ServiceTemplate?.ChecklistItems is { Count: > 0 } items)
        {
            foreach (var item in items.OrderBy(i => i.SortOrder))
            {
                _context.ServiceTicketChecklistItems.Add(new Features.ServiceTemplates.ServiceTicketChecklistItem
                {
                    ShopId = schedule.ShopId,
                    ServiceTicketId = ticket.Id,
                    Title = item.Title,
                    SortOrder = item.SortOrder,
                    IsCompleted = false
                });
            }
        }

        _context.ActivityLogs.Add(new Features.Activity.ActivityLog
        {
            ShopId = schedule.ShopId,
            EntityType = "ServiceTicket",
            EntityId = ticket.Id,
            ActionType = "PreventiveTicketCreated",
            Title = ticket.TicketNumber,
            Description = $"Auto-created preventive maintenance ticket {ticket.TicketNumber} from schedule '{schedule.Title}'."
        });

        await _context.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public static void AdvanceNextDue(MaintenanceSchedule schedule, DateTimeOffset completedAt, decimal? completedMeter = null)
    {
        schedule.LastCompletedAt = completedAt;

        if (schedule.TriggerType is MaintenanceTriggerType.Calendar or MaintenanceTriggerType.Both
            && schedule.IntervalValue is > 0
            && schedule.IntervalUnit.HasValue)
        {
            schedule.NextDueAt = schedule.IntervalUnit.Value switch
            {
                MaintenanceIntervalUnit.Days => completedAt.AddDays(schedule.IntervalValue.Value),
                MaintenanceIntervalUnit.Weeks => completedAt.AddDays(schedule.IntervalValue.Value * 7),
                MaintenanceIntervalUnit.Months => completedAt.AddMonths(schedule.IntervalValue.Value),
                _ => completedAt.AddMonths(1)
            };
        }

        if (schedule.TriggerType is MaintenanceTriggerType.Meter or MaintenanceTriggerType.Both
            && schedule.MeterInterval is > 0)
        {
            var baseMeter = completedMeter ?? schedule.ServiceAsset?.MeterReading ?? schedule.LastCompletedMeter ?? 0m;
            schedule.LastCompletedMeter = baseMeter;
            schedule.NextDueMeter = baseMeter + schedule.MeterInterval.Value;
        }
    }
}
