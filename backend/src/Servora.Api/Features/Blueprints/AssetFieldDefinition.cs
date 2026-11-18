using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Devices;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Blueprints;

public enum CustomFieldScope
{
    Asset,
    Ticket
}

public enum CustomFieldType
{
    Text,
    Number,
    Select,
    Boolean,
    Date,
    Multiline
}

public class AssetFieldDefinition : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public CustomFieldScope Scope { get; set; } = CustomFieldScope.Asset;
    public string FieldKey { get; set; } = string.Empty;
    public string LabelEn { get; set; } = string.Empty;
    public string LabelId { get; set; } = string.Empty;
    public CustomFieldType FieldType { get; set; } = CustomFieldType.Text;
    public string? OptionsJson { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
    public bool ShowOnIntake { get; set; } = true;
    public bool ShowOnDetail { get; set; } = true;
    public bool ShowOnPrint { get; set; }
    public bool ShowOnPublicTracking { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class AssetFieldValue : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ServiceAssetId { get; set; }
    public Device ServiceAsset { get; set; } = null!;
    public int FieldDefinitionId { get; set; }
    public AssetFieldDefinition FieldDefinition { get; set; } = null!;
    public string? Value { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class TicketFieldValue : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public int FieldDefinitionId { get; set; }
    public AssetFieldDefinition FieldDefinition { get; set; } = null!;
    public string? Value { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class AssetFieldDefinitionConfiguration : IEntityTypeConfiguration<AssetFieldDefinition>
{
    public void Configure(EntityTypeBuilder<AssetFieldDefinition> builder)
    {
        builder.ToTable("asset_field_definitions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.Scope).HasColumnName("scope").HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(x => x.FieldKey).HasColumnName("field_key").IsRequired().HasMaxLength(60);
        builder.Property(x => x.LabelEn).HasColumnName("label_en").IsRequired().HasMaxLength(120);
        builder.Property(x => x.LabelId).HasColumnName("label_id").IsRequired().HasMaxLength(120);
        builder.Property(x => x.FieldType).HasColumnName("field_type").HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(x => x.OptionsJson).HasColumnName("options_json").HasColumnType("text");
        builder.Property(x => x.IsRequired).HasColumnName("is_required").HasDefaultValue(false);
        builder.Property(x => x.IsSearchable).HasColumnName("is_searchable").HasDefaultValue(false);
        builder.Property(x => x.ShowOnIntake).HasColumnName("show_on_intake").HasDefaultValue(true);
        builder.Property(x => x.ShowOnDetail).HasColumnName("show_on_detail").HasDefaultValue(true);
        builder.Property(x => x.ShowOnPrint).HasColumnName("show_on_print").HasDefaultValue(false);
        builder.Property(x => x.ShowOnPublicTracking).HasColumnName("show_on_public_tracking").HasDefaultValue(false);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ShopId, x.Scope, x.FieldKey }).IsUnique();
        builder.HasIndex(x => new { x.ShopId, x.Scope, x.IsActive, x.SortOrder });

        builder.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AssetFieldValueConfiguration : IEntityTypeConfiguration<AssetFieldValue>
{
    public void Configure(EntityTypeBuilder<AssetFieldValue> builder)
    {
        builder.ToTable("asset_field_values");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.ServiceAssetId).HasColumnName("service_asset_id");
        builder.Property(x => x.FieldDefinitionId).HasColumnName("field_definition_id");
        builder.Property(x => x.Value).HasColumnName("value").HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ServiceAssetId, x.FieldDefinitionId }).IsUnique();
        builder.HasIndex(x => new { x.ShopId, x.FieldDefinitionId });

        builder.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ServiceAsset)
            .WithMany()
            .HasForeignKey(x => x.ServiceAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FieldDefinition)
            .WithMany()
            .HasForeignKey(x => x.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TicketFieldValueConfiguration : IEntityTypeConfiguration<TicketFieldValue>
{
    public void Configure(EntityTypeBuilder<TicketFieldValue> builder)
    {
        builder.ToTable("ticket_field_values");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.ServiceTicketId).HasColumnName("service_ticket_id");
        builder.Property(x => x.FieldDefinitionId).HasColumnName("field_definition_id");
        builder.Property(x => x.Value).HasColumnName("value").HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ServiceTicketId, x.FieldDefinitionId }).IsUnique();
        builder.HasIndex(x => new { x.ShopId, x.FieldDefinitionId });

        builder.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ServiceTicket)
            .WithMany()
            .HasForeignKey(x => x.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FieldDefinition)
            .WithMany()
            .HasForeignKey(x => x.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
