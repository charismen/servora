using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Dispatch;
using Servora.Api.Features.Maintenance;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.ServiceStatuses;
using Servora.Api.Features.ServiceTemplates;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Settings;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Blueprints;

public sealed class BlueprintProvisioningService
{
    private readonly AppDbContext _context;

    public BlueprintProvisioningService(AppDbContext context)
    {
        _context = context;
    }

    public async Task ApplyBlueprintAsync(
        Shop workspace,
        string blueprintKey,
        bool replaceExistingStatuses = false,
        bool replaceExistingFieldsAndChecklists = false,
        TerminologyConfigDto? customTerminology = null,
        WorkspaceFeatureConfig? customFeatureConfig = null,
        CancellationToken cancellationToken = default)
    {
        var blueprint = IndustryBlueprintCatalog.GetByKey(blueprintKey);
        workspace.IndustryBlueprintKey = blueprint.Key;
        workspace.TerminologyConfig = IndustryBlueprintCatalog.SerializeTerminology(customTerminology ?? blueprint.Terminology);
        workspace.FeatureConfig = IndustryBlueprintCatalog.SerializeFeatureConfig(
            customFeatureConfig ?? IndustryBlueprintCatalog.GetDefaultFeatureConfig(blueprint.Key, workspace.OperatingMode));

        // 1. Seed / align workspace profile settings
        await EnsureWorkspaceSettingsAsync(workspace, blueprint, cancellationToken);

        // 2. Service Statuses
        var existingStatuses = await _context.ServiceStatuses
            .Where(s => s.ShopId == workspace.Id)
            .ToListAsync(cancellationToken);

        var hasTickets = await _context.ServiceTickets
            .AnyAsync(t => t.ShopId == workspace.Id, cancellationToken);

        if (existingStatuses.Count == 0 || (replaceExistingStatuses && !hasTickets))
        {
            if (existingStatuses.Count > 0 && replaceExistingStatuses && !hasTickets)
            {
                _context.ServiceStatuses.RemoveRange(existingStatuses);
                await _context.SaveChangesAsync(cancellationToken);
            }

            foreach (var status in blueprint.Statuses)
            {
                _context.ServiceStatuses.Add(new ServiceStatus
                {
                    ShopId = workspace.Id,
                    Name = status.Name,
                    Color = status.Color,
                    SortOrder = status.SortOrder,
                    IsInitial = status.IsInitial,
                    IsBlocked = status.IsBlocked,
                    IsFinal = status.IsFinal,
                    IsCancelled = status.IsCancelled
                });
            }
        }

        // 3. Dynamic Custom Fields
        var existingFields = await _context.AssetFieldDefinitions
            .Where(f => f.ShopId == workspace.Id)
            .ToListAsync(cancellationToken);

        if (replaceExistingFieldsAndChecklists)
        {
            foreach (var existing in existingFields)
            {
                existing.IsActive = false;
            }
        }

        var existingByKey = existingFields.ToDictionary(
            f => $"{f.Scope}:{f.FieldKey}",
            StringComparer.OrdinalIgnoreCase);

        var sortIndex = 1;
        foreach (var field in blueprint.Fields)
        {
            var compositeKey = $"{field.Scope}:{field.FieldKey}";
            if (existingByKey.TryGetValue(compositeKey, out var existing))
            {
                existing.IsActive = true;
                existing.SortOrder = sortIndex++;
                continue;
            }

            _context.AssetFieldDefinitions.Add(new AssetFieldDefinition
            {
                ShopId = workspace.Id,
                Scope = field.Scope,
                FieldKey = field.FieldKey,
                LabelEn = field.LabelEn,
                LabelId = field.LabelId,
                FieldType = field.FieldType,
                OptionsJson = field.Options is { Length: > 0 }
                    ? JsonSerializer.Serialize(field.Options, IndustryBlueprintCatalog.JsonOptions)
                    : null,
                IsRequired = field.IsRequired,
                IsSearchable = field.IsSearchable,
                ShowOnIntake = field.ShowOnIntake,
                ShowOnDetail = field.ShowOnDetail,
                ShowOnPrint = field.ShowOnPrint,
                ShowOnPublicTracking = field.ShowOnPublicTracking,
                SortOrder = sortIndex++,
                IsActive = true
            });
        }

        // 4. Inspection Checklist Templates
        var existingChecklists = await _context.InspectionChecklistTemplates
            .Where(c => c.ShopId == workspace.Id)
            .ToListAsync(cancellationToken);

        if (replaceExistingFieldsAndChecklists)
        {
            foreach (var existing in existingChecklists)
            {
                existing.IsActive = false;
            }
        }

        var checklistByKey = existingChecklists.ToDictionary(c => c.ItemKey, StringComparer.OrdinalIgnoreCase);
        var checklistOrder = 1;
        foreach (var item in blueprint.Checklists)
        {
            if (checklistByKey.TryGetValue(item.ItemKey, out var existing))
            {
                existing.IsActive = true;
                existing.SortOrder = checklistOrder++;
                continue;
            }

            _context.InspectionChecklistTemplates.Add(new InspectionChecklistTemplate
            {
                ShopId = workspace.Id,
                ItemKey = item.ItemKey,
                LabelEn = item.LabelEn,
                LabelId = item.LabelId,
                Category = item.Category,
                SortOrder = checklistOrder++,
                IsRequired = item.IsRequired,
                IsActive = true
            });
        }

        // 5. Starter Service Templates (only if none exist yet for this workspace)
        var hasTemplates = await _context.ServiceTemplates
            .AnyAsync(t => t.ShopId == workspace.Id, cancellationToken);

        if (!hasTemplates)
        {
            foreach (var tmpl in blueprint.ServiceTemplates)
            {
                var entity = new ServiceTemplate
                {
                    ShopId = workspace.Id,
                    Name = tmpl.Name,
                    Description = tmpl.Description,
                    IsActive = true
                };

                var stepOrder = 1;
                foreach (var step in tmpl.ChecklistItems)
                {
                    entity.ChecklistItems.Add(new ServiceTemplateChecklistItem
                    {
                        ShopId = workspace.Id,
                        Title = step,
                        SortOrder = stepOrder++
                    });
                }

                _context.ServiceTemplates.Add(entity);
            }
        }

        // 5b. Starter Service Resources (e.g. Bays / Service Vans for Automotive & HVAC blueprints)
        if (blueprint.Resources is { Count: > 0 })
        {
            var existingResourceNames = await _context.ServiceResources
                .Where(r => r.ShopId == workspace.Id)
                .Select(r => r.Name)
                .ToListAsync(cancellationToken);

            if (existingResourceNames.Count == 0)
            {
                var resourceOrder = 1;
                foreach (var res in blueprint.Resources)
                {
                    _context.ServiceResources.Add(new ServiceResource
                    {
                        ShopId = workspace.Id,
                        Name = res.Name,
                        Code = res.Code,
                        ResourceType = res.ResourceType,
                        Color = res.Color,
                        SortOrder = resourceOrder++,
                        IsActive = true
                    });
                }
            }
        }

        // 6. Starter Notification Templates (if none exist yet)
        var hasNotifications = await _context.NotificationTemplates
            .AnyAsync(n => n.ShopId == workspace.Id, cancellationToken);

        if (!hasNotifications)
        {
            _context.NotificationTemplates.AddRange(
                new NotificationTemplate
                {
                    ShopId = workspace.Id,
                    Key = "ticket_received",
                    Name = "Ticket Received",
                    Channel = NotificationChannel.WhatsApp,
                    Locale = "en",
                    Body = "Hello {{customer_name}}, ticket {{ticket_number}} for {{device_name}} has been registered. Track progress: {{tracking_url}}",
                    IsActive = true
                },
                new NotificationTemplate
                {
                    ShopId = workspace.Id,
                    Key = "ready_for_pickup",
                    Name = "Ready / Completed",
                    Channel = NotificationChannel.WhatsApp,
                    Locale = "en",
                    Body = "Hello {{customer_name}}, ticket {{ticket_number}} ({{device_name}}) is now ready. Details: {{tracking_url}}",
                    IsActive = true
                });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SeedSampleDataForBlueprintAsync(Shop workspace, CancellationToken cancellationToken = default)
    {
        var blueprint = IndustryBlueprintCatalog.GetByKey(workspace.IndustryBlueprintKey);

        var hasCustomers = await _context.Customers
            .AnyAsync(c => c.ShopId == workspace.Id && c.DeletedAt == null, cancellationToken);

        if (hasCustomers)
        {
            return;
        }

        var isInternal = workspace.OperatingMode == OperatingMode.InternalMaintenance;

        var requester = new Customer
        {
            ShopId = workspace.Id,
            RequesterType = isInternal ? RequesterType.Department : RequesterType.Customer,
            FullName = isInternal ? "Operations & Facilities Dept" : "Alex Morgan",
            PhoneNumber = isInternal ? "+62 21-555-0100" : "+62 812-3456-7890",
            Email = isInternal ? "ops.dept@servora.local" : "alex.morgan@example.com",
            Address = isInternal ? "HQ Building, Floor 2" : "123 Meridian Street",
            CompanyName = isInternal ? workspace.Name : "Meridian Retail",
            DepartmentName = isInternal ? "Operations & Facilities" : null,
            DepartmentCode = isInternal ? "OPS-01" : null,
            CostCenter = isInternal ? "CC-100" : null,
            Notes = "Sample starter record created during onboarding."
        };
        _context.Customers.Add(requester);
        await _context.SaveChangesAsync(cancellationToken);

        var location = new CustomerLocation
        {
            ShopId = workspace.Id,
            CustomerId = requester.Id,
            Label = isInternal ? "Main Plant — Utility Room" : "Primary Site",
            Address = requester.Address ?? "Main Site Address",
            City = "Jakarta",
            ContactName = requester.FullName,
            ContactPhone = requester.PhoneNumber,
            IsDefault = true
        };
        _context.CustomerLocations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);

        var (brand, model, primaryId, secondaryId, meter) = blueprint.Key switch
        {
            "mobile_phone" => ("Apple", "iPhone 15 Pro", "356938035643809", "Natural Titanium", 140m),
            "automotive_vehicle" => ("Toyota", "Avanza 1.5 G", "B 1234 SRV", "MHFBA3B30P1000421", 42500m),
            "hvac_ac" => ("Daikin", "Inverter Split 2 PK", "DK-IN-88421", "DK-OUT-88422", 1850m),
            "facility_internal" => ("Caterpillar", "Backup Generator 150kVA", "FAC-GEN-001", "Utility Bay B1", 620m),
            _ => ("Lenovo", "ThinkPad X1 Carbon Gen 11", "PF4X9021", "IT-NB-042", 420m)
        };

        var asset = new Device
        {
            ShopId = workspace.Id,
            CustomerId = requester.Id,
            DeviceType = blueprint.DefaultAssetCategory,
            Brand = brand,
            Model = model,
            SerialNumber = primaryId,
            IdentifierLabel = secondaryId,
            MeterReading = meter,
            MeterUnit = blueprint.DefaultMeterUnit,
            SiteLocationId = location.Id,
            Notes = "Starter demo service asset."
        };
        _context.Devices.Add(asset);
        await _context.SaveChangesAsync(cancellationToken);

        var initialStatus = await _context.ServiceStatuses
            .Where(s => s.ShopId == workspace.Id)
            .OrderByDescending(s => s.IsInitial)
            .ThenBy(s => s.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (initialStatus is not null)
        {
            var ticket = new ServiceTicket
            {
                ShopId = workspace.Id,
                CustomerId = requester.Id,
                DeviceId = asset.Id,
                StatusId = initialStatus.Id,
                TicketNumber = $"{blueprint.DefaultTicketPrefix}-{DateTimeOffset.UtcNow:yyyyMMdd}-0001",
                PublicTrackingToken = Guid.NewGuid().ToString("N")[..24],
                PublicTrackingEnabled = !isInternal,
                CustomerNameSnapshot = requester.FullName,
                CustomerPhoneSnapshot = requester.PhoneNumber,
                DeviceSnapshot = $"{asset.Brand} {asset.Model} ({asset.SerialNumber})",
                Priority = ServiceTicketPriority.Normal,
                IsInternalWorkOrder = isInternal,
                DepartmentCode = requester.DepartmentCode,
                CostCenter = requester.CostCenter,
                ServiceLocationType = blueprint.Key is "hvac_ac" or "facility_internal"
                    ? ServiceLocationType.OnSite
                    : ServiceLocationType.InShop,
                SiteLocationId = location.Id,
                SiteAddressSnapshot = location.Address,
                Complaint = blueprint.Key switch
                {
                    "automotive_vehicle" => "Periodic 40,000 km service & front brake inspection.",
                    "hvac_ac" => "Quarterly preventive coil wash & refrigerant pressure check.",
                    "facility_internal" => "Scheduled preventive inspection and load test.",
                    "mobile_phone" => "Battery drains quickly and screen glass has hairline crack.",
                    _ => "Routine diagnostic check and preventive thermal service."
                },
                PaymentStatus = isInternal ? PaymentStatus.Waived : PaymentStatus.Unpaid
            };
            _context.ServiceTickets.Add(ticket);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Seed one sample preventive maintenance schedule
        var firstTemplate = await _context.ServiceTemplates
            .Where(t => t.ShopId == workspace.Id)
            .OrderBy(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        _context.MaintenanceSchedules.Add(new MaintenanceSchedule
        {
            ShopId = workspace.Id,
            ServiceAssetId = asset.Id,
            ServiceTemplateId = firstTemplate?.Id,
            Title = $"Preventive Maintenance — {asset.Brand} {asset.Model}",
            Description = "Auto-seeded recurring maintenance schedule from industry blueprint.",
            TriggerType = blueprint.Key == "automotive_vehicle" ? MaintenanceTriggerType.Both : MaintenanceTriggerType.Calendar,
            IntervalValue = 3,
            IntervalUnit = MaintenanceIntervalUnit.Months,
            MeterInterval = blueprint.Key == "automotive_vehicle" ? 10000m : null,
            LastCompletedMeter = asset.MeterReading,
            NextDueMeter = blueprint.Key == "automotive_vehicle" && asset.MeterReading.HasValue ? asset.MeterReading.Value + 10000m : null,
            NextDueAt = DateTimeOffset.UtcNow.AddDays(5),
            LeadTimeDays = 7,
            AutoCreateTicket = false,
            DefaultLocationType = blueprint.Key is "hvac_ac" or "facility_internal" ? ServiceLocationType.OnSite : ServiceLocationType.InShop,
            Status = MaintenanceScheduleStatus.Active
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureWorkspaceSettingsAsync(Shop workspace, IndustryBlueprintDefinition blueprint, CancellationToken cancellationToken)
    {
        var existingKeys = await _context.AppSettings
            .Where(s => s.ShopId == workspace.Id)
            .Select(s => s.Key)
            .ToListAsync(cancellationToken);

        var existingSet = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, defaultValue) in ShopProfileSettingKeys.Defaults)
        {
            if (existingSet.Contains(key)) continue;

            var value = key switch
            {
                ShopProfileSettingKeys.ShopName => workspace.Name,
                ShopProfileSettingKeys.PhoneNumber => workspace.PhoneNumber ?? defaultValue,
                ShopProfileSettingKeys.Address => workspace.Address ?? defaultValue,
                ShopProfileSettingKeys.CurrencyCode => workspace.CurrencyCode,
                ShopProfileSettingKeys.OperationalTimeZone => workspace.TimeZone,
                ShopProfileSettingKeys.TicketPrefix => blueprint.DefaultTicketPrefix,
                _ => defaultValue
            };

            _context.AppSettings.Add(new AppSetting
            {
                ShopId = workspace.Id,
                Key = key,
                Value = value
            });
        }
    }
}
