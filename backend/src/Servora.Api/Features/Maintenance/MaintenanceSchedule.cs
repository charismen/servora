using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.ServiceTemplates;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Maintenance;

public enum MaintenanceTriggerType
{
    Calendar,
    Meter,
    Both
}

public enum MaintenanceIntervalUnit
{
    Days,
    Weeks,
    Months
}

public enum MaintenanceScheduleStatus
{
    Active,
    Paused,
    Completed
}

public class MaintenanceSchedule : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ServiceAssetId { get; set; }
    public Device ServiceAsset { get; set; } = null!;
    public int? ServiceTemplateId { get; set; }
    public ServiceTemplate? ServiceTemplate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MaintenanceTriggerType TriggerType { get; set; } = MaintenanceTriggerType.Calendar;
    public int? IntervalValue { get; set; }
    public MaintenanceIntervalUnit? IntervalUnit { get; set; }
    public decimal? MeterInterval { get; set; }
    public decimal? LastCompletedMeter { get; set; }
    public decimal? NextDueMeter { get; set; }
    public DateTimeOffset? LastCompletedAt { get; set; }
    public DateTimeOffset? NextDueAt { get; set; }
    public int LeadTimeDays { get; set; } = 7;
    public bool AutoCreateTicket { get; set; }
    public int? AssignedTechnicianUserId { get; set; }
    public ApplicationUser? AssignedTechnicianUser { get; set; }
    public ServiceLocationType DefaultLocationType { get; set; } = ServiceLocationType.InShop;
    public int? LastGeneratedTicketId { get; set; }
    public ServiceTicket? LastGeneratedTicket { get; set; }
    public DateTimeOffset? LastNotifiedAt { get; set; }
    public MaintenanceScheduleStatus Status { get; set; } = MaintenanceScheduleStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class MaintenanceScheduleConfiguration : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> builder)
    {
        builder.ToTable("maintenance_schedules");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.ServiceAssetId).HasColumnName("service_asset_id");
        builder.Property(x => x.ServiceTemplateId).HasColumnName("service_template_id");
        builder.Property(x => x.Title).HasColumnName("title").IsRequired().HasMaxLength(180);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(x => x.TriggerType).HasColumnName("trigger_type").HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(x => x.IntervalValue).HasColumnName("interval_value");
        builder.Property(x => x.IntervalUnit).HasColumnName("interval_unit").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.MeterInterval).HasColumnName("meter_interval").HasPrecision(14, 2);
        builder.Property(x => x.LastCompletedMeter).HasColumnName("last_completed_meter").HasPrecision(14, 2);
        builder.Property(x => x.NextDueMeter).HasColumnName("next_due_meter").HasPrecision(14, 2);
        builder.Property(x => x.LastCompletedAt).HasColumnName("last_completed_at");
        builder.Property(x => x.NextDueAt).HasColumnName("next_due_at");
        builder.Property(x => x.LeadTimeDays).HasColumnName("lead_time_days").HasDefaultValue(7);
        builder.Property(x => x.AutoCreateTicket).HasColumnName("auto_create_ticket").HasDefaultValue(false);
        builder.Property(x => x.AssignedTechnicianUserId).HasColumnName("assigned_technician_user_id");
        builder.Property(x => x.DefaultLocationType).HasColumnName("default_location_type").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(ServiceLocationType.InShop);
        builder.Property(x => x.LastGeneratedTicketId).HasColumnName("last_generated_ticket_id");
        builder.Property(x => x.LastNotifiedAt).HasColumnName("last_notified_at");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(MaintenanceScheduleStatus.Active);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ShopId, x.Status, x.NextDueAt });
        builder.HasIndex(x => new { x.ShopId, x.ServiceAssetId });

        builder.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ServiceAsset)
            .WithMany()
            .HasForeignKey(x => x.ServiceAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ServiceTemplate)
            .WithMany()
            .HasForeignKey(x => x.ServiceTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AssignedTechnicianUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedTechnicianUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.LastGeneratedTicket)
            .WithMany()
            .HasForeignKey(x => x.LastGeneratedTicketId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
