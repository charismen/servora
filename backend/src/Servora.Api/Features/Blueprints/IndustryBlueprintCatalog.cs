using System.Text.Json;
using System.Text.Json.Serialization;
using Servora.Api.Features.Dispatch;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;

namespace Servora.Api.Features.Blueprints;

public enum SecurityCredentialMode
{
    None,
    PasscodeOnly,
    PasscodeAndPattern
}

public sealed record WorkspaceFeatureConfig(
    bool ShowSecurityCredentials,
    SecurityCredentialMode SecurityCredentialMode,
    bool ShowAccessories,
    string[] QuickAccessoriesEn,
    string[] QuickAccessoriesId,
    bool ShowPreRepairChecklist,
    bool ShowPhysicalConditionNotes,
    bool ShowWarrantySection,
    bool ShowBillingAndPayments,
    bool ShowCustomerApprovals,
    bool EnableFieldService,
    ServiceLocationType DefaultServiceLocationType,
    bool EnablePreventiveMaintenance,
    string[] AttachmentTypes);

public sealed record TerminologyLabels(
    string AssetSingular,
    string AssetPlural,
    string TicketSingular,
    string TicketPlural,
    string RequesterSingular,
    string RequesterPlural,
    string PrimaryIdentifierLabel,
    string SecondaryIdentifierLabel,
    string MeterLabel);

public sealed record TerminologyConfigDto(
    TerminologyLabels En,
    TerminologyLabels Id);

public sealed record BlueprintStatusSeed(
    string Name,
    string Color,
    int SortOrder,
    bool IsInitial = false,
    bool IsBlocked = false,
    bool IsFinal = false,
    bool IsCancelled = false);

public sealed record BlueprintFieldSeed(
    CustomFieldScope Scope,
    string FieldKey,
    string LabelEn,
    string LabelId,
    CustomFieldType FieldType,
    string[]? Options = null,
    bool IsRequired = false,
    bool IsSearchable = false,
    bool ShowOnIntake = true,
    bool ShowOnDetail = true,
    bool ShowOnPrint = false,
    bool ShowOnPublicTracking = false);

public sealed record BlueprintChecklistSeed(
    string ItemKey,
    string LabelEn,
    string LabelId,
    string Category,
    bool IsRequired = false);

public sealed record BlueprintTemplateSeed(
    string Name,
    string Category,
    string Description,
    decimal EstimatedPrice,
    int EstimatedDurationMinutes,
    int WarrantyDays,
    string[] ChecklistItems);

public sealed record BlueprintResourceSeed(
    string Name,
    string Code,
    ServiceResourceType ResourceType,
    string Color = "#b45039");

public sealed record IndustryBlueprintDefinition(
    string Key,
    string NameEn,
    string NameId,
    string DescriptionEn,
    string DescriptionId,
    OperatingMode RecommendedMode,
    string DefaultMeterUnit,
    string DefaultTicketPrefix,
    string DefaultAssetCategory,
    TerminologyConfigDto Terminology,
    WorkspaceFeatureConfig FeatureConfig,
    IReadOnlyList<BlueprintStatusSeed> Statuses,
    IReadOnlyList<BlueprintFieldSeed> Fields,
    IReadOnlyList<BlueprintChecklistSeed> Checklists,
    IReadOnlyList<BlueprintTemplateSeed> ServiceTemplates,
    IReadOnlyList<BlueprintResourceSeed>? Resources = null);

public static class IndustryBlueprintCatalog
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static readonly IReadOnlyList<IndustryBlueprintDefinition> All =
    [
        new IndustryBlueprintDefinition(
            Key: "it_computer",
            NameEn: "Laptop & PC Repair",
            NameId: "Servis Laptop & PC",
            DescriptionEn: "Bench repair for laptops, desktops, workstations, and peripherals with diagnostics, accessories, and BIOS/boot checklists.",
            DescriptionId: "Perbaikan laptop, komputer desktop, workstation, dan periferal lengkap dengan diagnostik, aksesoris, dan ceklis boot/BIOS.",
            RecommendedMode: OperatingMode.Commercial,
            DefaultMeterUnit: "hours",
            DefaultTicketPrefix: "IT",
            DefaultAssetCategory: "Laptop",
            Terminology: new TerminologyConfigDto(
                En: new TerminologyLabels("Device", "Devices", "Service Ticket", "Service Tickets", "Customer", "Customers", "Serial Number", "Asset Label / Tag", "Runtime Hours"),
                Id: new TerminologyLabels("Perangkat", "Perangkat", "Tiket Servis", "Tiket Servis", "Pelanggan", "Pelanggan", "Nomor Seri", "Label / Tag Aset", "Jam Pemakaian")),
            FeatureConfig: new WorkspaceFeatureConfig(
                ShowSecurityCredentials: true,
                SecurityCredentialMode: SecurityCredentialMode.PasscodeOnly,
                ShowAccessories: true,
                QuickAccessoriesEn: ["Charger / Power Adapter", "Laptop Bag / Sleeve", "Power Cable", "Mouse / Dongle", "Original Box"],
                QuickAccessoriesId: ["Charger / Adaptor", "Tas / Sleeve Laptop", "Kabel Power", "Mouse / Dongle", "Dus / Kotak"],
                ShowPreRepairChecklist: true,
                ShowPhysicalConditionNotes: true,
                ShowWarrantySection: true,
                ShowBillingAndPayments: true,
                ShowCustomerApprovals: true,
                EnableFieldService: false,
                DefaultServiceLocationType: ServiceLocationType.InShop,
                EnablePreventiveMaintenance: true,
                AttachmentTypes: ["DeviceFront", "DeviceBack", "Screen", "Keyboard", "Accessories", "Damage", "SerialOrTag", "Receipt", "Other"]),
            Statuses:
            [
                new("Received", "#64748b", 1, IsInitial: true),
                new("Diagnosing", "#2563eb", 2),
                new("Waiting Approval", "#d97706", 3, IsBlocked: true),
                new("In Repair", "#7c3aed", 4),
                new("Testing", "#0891b2", 5),
                new("Ready for Pickup", "#0d9488", 6),
                new("Completed", "#059669", 7, IsFinal: true),
                new("Cancelled", "#dc2626", 8, IsCancelled: true)
            ],
            Fields:
            [
                new(CustomFieldScope.Asset, "cpu_ram_specs", "CPU & RAM Specs", "Spesifikasi CPU & RAM", CustomFieldType.Text, IsSearchable: true, ShowOnPrint: true),
                new(CustomFieldScope.Asset, "storage_spec", "Storage (SSD/HDD)", "Penyimpanan (SSD/HDD)", CustomFieldType.Text, ShowOnPrint: true),
                new(CustomFieldScope.Asset, "os_version", "Operating System", "Sistem Operasi", CustomFieldType.Select, ["Windows 11", "Windows 10", "macOS", "Linux", "None / Unknown"]),
                new(CustomFieldScope.Ticket, "charger_included", "Charger Included", "Termasuk Charger", CustomFieldType.Boolean, ShowOnPrint: true, ShowOnPublicTracking: true),
                new(CustomFieldScope.Ticket, "bag_included", "Bag / Sleeve Included", "Termasuk Tas / Sleeve", CustomFieldType.Boolean, ShowOnPrint: true),
                new(CustomFieldScope.Ticket, "data_backup_confirmed", "Customer Confirmed Data Backup", "Konfirmasi Backup Data", CustomFieldType.Boolean, ShowOnPrint: true)
            ],
            Checklists:
            [
                new("power_boot", "Power & BIOS/UEFI Boot", "Daya & Boot BIOS/UEFI", "Core", true),
                new("display_hinge", "Display Panel & Hinges", "Layar & Engsel", "Hardware", true),
                new("keyboard_trackpad", "Keyboard & Trackpad", "Keyboard & Trackpad", "Input"),
                new("ports_charging", "USB, HDMI & Charging Ports", "Port USB, HDMI & Pengisian", "Ports"),
                new("battery_health", "Battery Condition & Swelling", "Kondisi & Kesehatan Baterai", "Power"),
                new("fan_thermals", "Fan Noise & Thermal Check", "Suara Kipas & Suhu", "Cooling")
            ],
            ServiceTemplates:
            [
                new("Deep Cleaning & Thermal Repaste", "Maintenance", "Internal dust removal, fan cleaning, and high-grade thermal compound replacement.", 175000, 90, 30, ["Inspect fan RPM", "Clean heatsink fins", "Apply thermal paste", "Run 15-min stress test"]),
                new("OS Installation & Driver Setup", "Software", "Clean operating system installation, updates, and essential hardware drivers.", 150000, 120, 14, ["Verify data backup", "Install OS", "Install chipset/GPU drivers", "Verify audio/Wi-Fi"]),
                new("SSD / RAM Upgrade", "Hardware", "Hardware installation, optional OS cloning, and memory stability verification.", 120000, 60, 30, ["Disconnect battery", "Install upgrade module", "Confirm capacity in BIOS", "Run diagnostic check"])
            ]),

        new IndustryBlueprintDefinition(
            Key: "mobile_phone",
            NameEn: "Mobile Phone & Tablet Repair",
            NameId: "Servis HP & Tablet",
            DescriptionEn: "Smartphones and tablets with IMEI tracking, storage/color specs, screen/camera/liquid-damage intake checklists.",
            DescriptionId: "Perbaikan smartphone dan tablet dengan pelacakan IMEI, kapasitas penyimpanan/warna, serta ceklis layar, kamera, dan indikasi air.",
            RecommendedMode: OperatingMode.Commercial,
            DefaultMeterUnit: "cycles",
            DefaultTicketPrefix: "MOB",
            DefaultAssetCategory: "Smartphone",
            Terminology: new TerminologyConfigDto(
                En: new TerminologyLabels("Device", "Devices", "Repair Ticket", "Repair Tickets", "Customer", "Customers", "IMEI / Serial", "Color / Model Code", "Battery Cycles"),
                Id: new TerminologyLabels("Perangkat", "Perangkat", "Tiket Perbaikan", "Tiket Perbaikan", "Pelanggan", "Pelanggan", "IMEI / Nomor Seri", "Warna / Kode Model", "Siklus Baterai")),
            FeatureConfig: new WorkspaceFeatureConfig(
                ShowSecurityCredentials: true,
                SecurityCredentialMode: SecurityCredentialMode.PasscodeAndPattern,
                ShowAccessories: true,
                QuickAccessoriesEn: ["Protective Case", "SIM Tray / SD Card", "Charging Cable", "Adapter", "Box"],
                QuickAccessoriesId: ["Casing / Pelindung", "Slot SIM / Kartu Memori", "Kabel Charger", "Kepala Charger", "Dus"],
                ShowPreRepairChecklist: true,
                ShowPhysicalConditionNotes: true,
                ShowWarrantySection: true,
                ShowBillingAndPayments: true,
                ShowCustomerApprovals: true,
                EnableFieldService: false,
                DefaultServiceLocationType: ServiceLocationType.InShop,
                EnablePreventiveMaintenance: false,
                AttachmentTypes: ["FrontView", "BackView", "Screen", "Accessories", "Damage", "BeforeRepair", "AfterRepair", "Receipt", "Other"]),
            Statuses:
            [
                new("Received", "#64748b", 1, IsInitial: true),
                new("Diagnosing", "#2563eb", 2),
                new("Waiting Approval", "#d97706", 3, IsBlocked: true),
                new("Waiting Parts", "#ea580c", 4, IsBlocked: true),
                new("In Repair", "#7c3aed", 5),
                new("QC Testing", "#0891b2", 6),
                new("Ready for Pickup", "#0d9488", 7),
                new("Completed", "#059669", 8, IsFinal: true),
                new("Cancelled", "#dc2626", 9, IsCancelled: true)
            ],
            Fields:
            [
                new(CustomFieldScope.Asset, "storage_capacity", "Storage Capacity", "Kapasitas Penyimpanan", CustomFieldType.Select, ["64GB", "128GB", "256GB", "512GB", "1TB"], ShowOnPrint: true),
                new(CustomFieldScope.Asset, "color_finish", "Color / Finish", "Warna", CustomFieldType.Text, ShowOnPrint: true),
                new(CustomFieldScope.Ticket, "sim_tray_present", "SIM Tray & Card Present", "Slot / Kartu SIM Terpasang", CustomFieldType.Boolean, ShowOnPrint: true),
                new(CustomFieldScope.Ticket, "liquid_indicator_triggered", "Liquid Damage Indicator Triggered", "Indikator Cairan Aktif", CustomFieldType.Boolean, ShowOnPrint: true, ShowOnPublicTracking: true),
                new(CustomFieldScope.Ticket, "find_my_disabled", "Find My / FRP Lock Disabled", "Find My / Kunci Akun Nonaktif", CustomFieldType.Boolean)
            ],
            Checklists:
            [
                new("screen_touch", "Screen Display & Touch Digitizer", "Tampilan Layar & Sentuhan", "Display", true),
                new("biometrics", "Face ID / Touch ID / Fingerprint", "Face ID / Sidik Jari", "Security", true),
                new("cameras", "Front & Rear Cameras", "Kamera Depan & Belakang", "Media"),
                new("audio_mic", "Speaker, Earpiece & Microphone", "Speaker & Mikrofon", "Audio"),
                new("charging_port", "Charging Port & Fast Charge", "Port Pengisian Daya", "Power"),
                new("liquid_check", "Liquid Damage Indicator Check", "Pemeriksaan Indikator Air", "Safety", true)
            ],
            ServiceTemplates:
            [
                new("Screen / LCD Assembly Replacement", "Display", "Replace cracked or unresponsive display module and reseal frame.", 450000, 90, 30, ["Test touch & TrueTone", "Transfer ear speaker/sensors", "Install adhesive seal", "Run full QC"]),
                new("Battery Replacement", "Power", "Replace degraded battery cell and calibrate power management.", 250000, 45, 90, ["Check old battery health", "Remove adhesive strips safely", "Install new battery", "Verify charging current"])
            ]),

        new IndustryBlueprintDefinition(
            Key: "automotive_vehicle",
            NameEn: "Motorbike & Car Workshop",
            NameId: "Bengkel Motor & Mobil",
            DescriptionEn: "Automotive & motorcycle repair workshops with license plate, VIN/chassis, odometer tracking, bay assignment, and walkaround checks.",
            DescriptionId: "Bengkel servis mobil & motor dengan nomor polisi, nomor rangka/VIN, pencatatan kilometer, nomor bay, dan ceklis keliling kendaraan.",
            RecommendedMode: OperatingMode.Commercial,
            DefaultMeterUnit: "km",
            DefaultTicketPrefix: "WO",
            DefaultAssetCategory: "Car",
            Terminology: new TerminologyConfigDto(
                En: new TerminologyLabels("Vehicle", "Vehicles", "Repair Order", "Repair Orders", "Customer", "Customers", "License Plate", "VIN / Chassis No.", "Odometer (km)"),
                Id: new TerminologyLabels("Kendaraan", "Kendaraan", "Perintah Kerja", "Perintah Kerja", "Pelanggan", "Pelanggan", "Nomor Polisi", "Nomor Rangka / VIN", "Odometer (km)")),
            FeatureConfig: new WorkspaceFeatureConfig(
                ShowSecurityCredentials: false,
                SecurityCredentialMode: SecurityCredentialMode.None,
                ShowAccessories: true,
                QuickAccessoriesEn: ["Vehicle Key / Fob", "Registration (STNK)", "Service Book", "Spare Tire / Tool Kit", "Helmet / Box"],
                QuickAccessoriesId: ["Kunci / Remote Kendaraan", "STNK", "Buku Servis", "Ban Serep / Tool Kit", "Helm / Box Motor"],
                ShowPreRepairChecklist: true,
                ShowPhysicalConditionNotes: true,
                ShowWarrantySection: true,
                ShowBillingAndPayments: true,
                ShowCustomerApprovals: true,
                EnableFieldService: false,
                DefaultServiceLocationType: ServiceLocationType.InShop,
                EnablePreventiveMaintenance: true,
                AttachmentTypes: ["ExteriorWalkaround", "Odometer", "EngineBay", "Damage", "BeforeRepair", "AfterRepair", "Receipt", "Other"]),
            Statuses:
            [
                new("Checked In", "#64748b", 1, IsInitial: true),
                new("Inspection", "#2563eb", 2),
                new("Waiting Approval", "#d97706", 3, IsBlocked: true),
                new("Waiting Parts", "#ea580c", 4, IsBlocked: true),
                new("In Bay", "#7c3aed", 5),
                new("Road Test", "#0891b2", 6),
                new("Ready for Pickup", "#0d9488", 7),
                new("Completed", "#059669", 8, IsFinal: true),
                new("Cancelled", "#dc2626", 9, IsCancelled: true)
            ],
            Fields:
            [
                new(CustomFieldScope.Asset, "engine_number", "Engine Number", "Nomor Mesin", CustomFieldType.Text, IsSearchable: true, ShowOnPrint: true),
                new(CustomFieldScope.Asset, "vehicle_year", "Model Year", "Tahun Pembuatan", CustomFieldType.Number, ShowOnPrint: true),
                new(CustomFieldScope.Asset, "vehicle_color", "Color", "Warna", CustomFieldType.Text, ShowOnPrint: true),
                new(CustomFieldScope.Ticket, "fuel_level", "Fuel Level at Intake", "Level Bahan Bakar Masuk", CustomFieldType.Select, ["Empty", "1/4", "1/2", "3/4", "Full"], ShowOnPrint: true),
                new(CustomFieldScope.Ticket, "bay_number", "Service Bay / Lift", "Nomor Bay / Lift", CustomFieldType.Text, ShowOnPublicTracking: true)
            ],
            Checklists:
            [
                new("body_walkaround", "Body Scratches & Dents Walkaround", "Cek Goresan & Penyok Bodi", "Exterior", true),
                new("tires_brakes", "Tires, Tread & Brake Pads", "Ban & Kampas Rem", "Safety", true),
                new("lights_signals", "Headlights, Brake Lights & Signals", "Lampu Utama, Rem & Sein", "Electrical"),
                new("fluids", "Engine Oil, Coolant & Brake Fluid", "Oli Mesin, Coolant & Minyak Rem", "Fluids", true),
                new("battery_starter", "Battery Voltage & Starter", "Tegangan Aki & Starter", "Electrical"),
                new("dashboard_warnings", "Dashboard Warning Indicators", "Indikator Peringatan Dashboard", "Diagnostics")
            ],
            ServiceTemplates:
            [
                new("Periodic Tune-Up & Oil Service", "Preventive", "Engine oil & filter change, air filter check, brake inspection, and fluid top-up.", 350000, 90, 14, ["Drain & refill engine oil", "Replace oil filter", "Check tire pressure", "Reset service indicator"]),
                new("Brake System Overhaul", "Safety", "Brake pad replacement, rotor check, caliper lubrication, and fluid bleed.", 450000, 120, 30, ["Measure pad thickness", "Inspect brake lines", "Replace pads & bleed fluid", "Road test braking"])
            ],
            Resources:
            [
                new("Bay 1", "BAY-1", ServiceResourceType.Bay, "#b45039"),
                new("Bay 2", "BAY-2", ServiceResourceType.Bay, "#2563eb")
            ]),

        new IndustryBlueprintDefinition(
            Key: "hvac_ac",
            NameEn: "AC & HVAC Service",
            NameId: "Servis AC & HVAC",
            DescriptionEn: "Field service for residential & commercial HVAC units with BTU/PK capacity, refrigerant tracking, site addresses, and multi-unit visits.",
            DescriptionId: "Layanan lapangan AC & HVAC rumah maupun komersial dengan kapasitas BTU/PK, jenis freon, lokasi pemasangan, dan kunjungan multi-unit.",
            RecommendedMode: OperatingMode.Commercial,
            DefaultMeterUnit: "hours",
            DefaultTicketPrefix: "HVAC",
            DefaultAssetCategory: "Split AC",
            Terminology: new TerminologyConfigDto(
                En: new TerminologyLabels("AC Unit", "AC Units", "Service Visit", "Service Visits", "Client", "Clients", "Indoor Unit Serial", "Outdoor Unit Serial", "Operating Hours"),
                Id: new TerminologyLabels("Unit AC", "Unit AC", "Kunjungan Servis", "Kunjungan Servis", "Klien", "Klien", "Seri Unit Indoor", "Seri Unit Outdoor", "Jam Operasi")),
            FeatureConfig: new WorkspaceFeatureConfig(
                ShowSecurityCredentials: false,
                SecurityCredentialMode: SecurityCredentialMode.None,
                ShowAccessories: true,
                QuickAccessoriesEn: ["Remote Control", "Mounting Bracket", "Drain Hose", "Power Plug / MCB"],
                QuickAccessoriesId: ["Remote Control", "Breket Dinding", "Selang Pembuangan", "Steker / MCB"],
                ShowPreRepairChecklist: true,
                ShowPhysicalConditionNotes: true,
                ShowWarrantySection: true,
                ShowBillingAndPayments: true,
                ShowCustomerApprovals: true,
                EnableFieldService: true,
                DefaultServiceLocationType: ServiceLocationType.OnSite,
                EnablePreventiveMaintenance: true,
                AttachmentTypes: ["IndoorUnit", "OutdoorUnit", "GaugeReading", "SiteLocation", "BeforeRepair", "AfterRepair", "Damage", "Receipt", "Other"]),
            Statuses:
            [
                new("Scheduled", "#64748b", 1, IsInitial: true),
                new("Dispatched", "#2563eb", 2),
                new("On Site", "#7c3aed", 3),
                new("Waiting Parts", "#d97706", 4, IsBlocked: true),
                new("Testing", "#0891b2", 5),
                new("Completed", "#059669", 6, IsFinal: true),
                new("Cancelled", "#dc2626", 7, IsCancelled: true)
            ],
            Fields:
            [
                new(CustomFieldScope.Asset, "unit_type", "HVAC Unit Type", "Tipe Unit AC", CustomFieldType.Select, ["Split Wall", "Cassette", "Floor Standing", "Ducted", "VRV / VRF", "Chiller"], IsRequired: true, ShowOnPrint: true, ShowOnPublicTracking: true),
                new(CustomFieldScope.Asset, "btu_pk_capacity", "Capacity (PK / BTU)", "Kapasitas (PK / BTU)", CustomFieldType.Select, ["0.5 PK", "1 PK", "1.5 PK", "2 PK", "2.5 PK", "3 PK", "5 PK+"], ShowOnPrint: true),
                new(CustomFieldScope.Asset, "refrigerant_type", "Refrigerant Type", "Jenis Freon / Refrigeran", CustomFieldType.Select, ["R32", "R410A", "R22", "R134a", "Other"], ShowOnPrint: true),
                new(CustomFieldScope.Asset, "room_location", "Room / Zone Location", "Lokasi Ruangan / Zona", CustomFieldType.Text, IsSearchable: true, ShowOnPrint: true),
                new(CustomFieldScope.Ticket, "suction_pressure_psi", "Suction Pressure (PSI)", "Tekanan Hisap (PSI)", CustomFieldType.Number, ShowOnPrint: true)
            ],
            Checklists:
            [
                new("airflow_fan", "Airflow & Blower Fan Speed", "Aliran Udara & Kipas Blower", "Airflow", true),
                new("compressor", "Compressor Amp Draw & Operation", "Arus & Operasi Kompresor", "Mechanical", true),
                new("drainage", "Condensate Drain Line & Pan", "Saluran Pembuangan Air", "Drainage", true),
                new("refrigerant_pressure", "Refrigerant Pressure & Leak Check", "Tekanan & Kebocoran Freon", "Refrigeration"),
                new("coils_clean", "Evaporator & Condenser Coils", "Kebersihan Koil Indoor & Outdoor", "Cleaning"),
                new("electrical_cap", "Electrical Terminals & Capacitor", "Kelistrikan & Kapasitor", "Electrical")
            ],
            ServiceTemplates:
            [
                new("Standard AC Wash & Preventive Check", "Preventive", "High-pressure indoor/outdoor coil cleaning, drain flush, and operating amp check.", 125000, 60, 14, ["Wash evaporator & filter", "Flush drain pipe", "Wash outdoor condenser", "Measure outlet temp & amps"]),
                new("Refrigerant Top-Up & Leak Test", "Repair", "Pressure test lines, seal minor flare leak, vacuum, and recharge refrigerant.", 325000, 90, 30, ["Inspect flare fittings", "Check manifold pressure", "Charge to spec", "Verify cooling delta-T"])
            ],
            Resources:
            [
                new("Service Van 1", "VAN-1", ServiceResourceType.Vehicle, "#0891b2"),
                new("Service Van 2", "VAN-2", ServiceResourceType.Vehicle, "#059669")
            ]),

        new IndustryBlueprintDefinition(
            Key: "facility_internal",
            NameEn: "Facility & Internal Equipment Maintenance",
            NameId: "Pemeliharaan Fasilitas & Aset Internal",
            DescriptionEn: "Internal corporate IT, facility, plant, or fleet maintenance teams with department/cost-center work orders, SLA tracking, and preventive schedules.",
            DescriptionId: "Tim pemeliharaan internal perusahaan (IT, gedung, pabrik, atau armada) dengan perintah kerja departemen/cost center, target SLA, dan jadwal preventif.",
            RecommendedMode: OperatingMode.InternalMaintenance,
            DefaultMeterUnit: "hours",
            DefaultTicketPrefix: "CMMS",
            DefaultAssetCategory: "Facility Equipment",
            Terminology: new TerminologyConfigDto(
                En: new TerminologyLabels("Equipment", "Equipment", "Work Order", "Work Orders", "Department", "Departments", "Asset Tag", "Location / Serial", "Runtime Hours"),
                Id: new TerminologyLabels("Peralatan", "Peralatan", "Perintah Kerja", "Perintah Kerja", "Departemen", "Departemen", "Tag Aset", "Lokasi / Seri", "Jam Operasi")),
            FeatureConfig: new WorkspaceFeatureConfig(
                ShowSecurityCredentials: false,
                SecurityCredentialMode: SecurityCredentialMode.None,
                ShowAccessories: false,
                QuickAccessoriesEn: ["Access Key / Card", "Manual Book", "Spare Fuse / Key", "Safety Lock"],
                QuickAccessoriesId: ["Kunci / Kartu Akses", "Buku Manual", "Sekring Cadangan", "Gembok LOTO"],
                ShowPreRepairChecklist: true,
                ShowPhysicalConditionNotes: true,
                ShowWarrantySection: false,
                ShowBillingAndPayments: false,
                ShowCustomerApprovals: false,
                EnableFieldService: true,
                DefaultServiceLocationType: ServiceLocationType.OnSite,
                EnablePreventiveMaintenance: true,
                AttachmentTypes: ["SiteLocation", "EquipmentTag", "SafetyCheck", "GaugeReading", "Damage", "BeforeRepair", "AfterRepair", "Other"]),
            Statuses:
            [
                new("Logged", "#64748b", 1, IsInitial: true),
                new("Triaged", "#2563eb", 2),
                new("Assigned", "#0891b2", 3),
                new("In Progress", "#7c3aed", 4),
                new("Waiting Parts", "#d97706", 5, IsBlocked: true),
                new("Verification", "#0d9488", 6),
                new("Closed", "#059669", 7, IsFinal: true),
                new("Cancelled", "#dc2626", 8, IsCancelled: true)
            ],
            Fields:
            [
                new(CustomFieldScope.Asset, "building_floor_room", "Building / Floor / Room", "Gedung / Lantai / Ruangan", CustomFieldType.Text, IsRequired: true, IsSearchable: true, ShowOnPrint: true),
                new(CustomFieldScope.Asset, "criticality", "Equipment Criticality", "Tingkat Kritikalitas", CustomFieldType.Select, ["Low", "Medium", "High", "Mission Critical"], ShowOnPrint: true),
                new(CustomFieldScope.Asset, "warranty_vendor", "Vendor / AMC Provider", "Vendor / Penyedia Kontrak", CustomFieldType.Text),
                new(CustomFieldScope.Ticket, "downtime_minutes", "Downtime Duration (mins)", "Durasi Downtime (menit)", CustomFieldType.Number, ShowOnPrint: true),
                new(CustomFieldScope.Ticket, "safety_permit_required", "Safety / LOTO Permit Required", "Perlu Izin Keselamatan / LOTO", CustomFieldType.Boolean, ShowOnPrint: true)
            ],
            Checklists:
            [
                new("safety_loto", "Power Isolation & Safety Lockout (LOTO)", "Isolasi Daya & Prosedur Keselamatan", "Safety", true),
                new("structural_mount", "Mounting & Structural Integrity", "Integritas Pemasangan & Struktur", "Mechanical"),
                new("calibration", "Operating Calibration & Output", "Kalibrasi & Output Operasi", "Operations"),
                new("hazard_env", "Environmental & Leak/Hazard Inspection", "Pemeriksaan Kebocoran & Bahaya", "Safety"),
                new("post_signoff", "Post-Maintenance Safety Sign-off", "Verifikasi Akhir & Serah Terima", "Sign-off", true)
            ],
            ServiceTemplates:
            [
                new("Monthly Preventive Equipment Inspection", "Preventive", "Routine safety, lubrication, calibration, and operating log verification.", 0, 60, 0, ["Inspect power & controls", "Check wear components", "Record runtime meter", "Department sign-off"]),
                new("Urgent Breakdown Response", "Corrective", "Rapid triage, fault isolation, temporary mitigation, and corrective repair.", 0, 120, 0, ["Verify safety lockout", "Diagnose root cause", "Replace faulty component", "Confirm return to service"])
            ]),

        new IndustryBlueprintDefinition(
            Key: "custom",
            NameEn: "Custom / Blank Blueprint",
            NameId: "Blueprint Kustom / Kosong",
            DescriptionEn: "Minimal starter workflow so any service, repair, or maintenance operation can define its own fields, checklists, and terminology.",
            DescriptionId: "Alur kerja minimal agar bisnis servis atau tim pemeliharaan apa pun dapat menyusun field, ceklis, dan istilah sendiri.",
            RecommendedMode: OperatingMode.Hybrid,
            DefaultMeterUnit: "units",
            DefaultTicketPrefix: "SRV",
            DefaultAssetCategory: "Equipment",
            Terminology: new TerminologyConfigDto(
                En: new TerminologyLabels("Asset", "Assets", "Service Ticket", "Service Tickets", "Requester", "Requesters", "Primary ID", "Secondary ID", "Usage Meter"),
                Id: new TerminologyLabels("Aset", "Aset", "Tiket Layanan", "Tiket Layanan", "Pemohon", "Pemohon", "ID Utama", "ID Sekunder", "Meter Pemakaian")),
            FeatureConfig: new WorkspaceFeatureConfig(
                ShowSecurityCredentials: false,
                SecurityCredentialMode: SecurityCredentialMode.None,
                ShowAccessories: true,
                QuickAccessoriesEn: ["Power Adapter / Cord", "Manual / Key", "Carrying Case", "Spare Accessory"],
                QuickAccessoriesId: ["Adaptor / Kabel", "Buku Manual / Kunci", "Tas / Kotak", "Aksesoris Tambahan"],
                ShowPreRepairChecklist: true,
                ShowPhysicalConditionNotes: true,
                ShowWarrantySection: true,
                ShowBillingAndPayments: true,
                ShowCustomerApprovals: true,
                EnableFieldService: true,
                DefaultServiceLocationType: ServiceLocationType.InShop,
                EnablePreventiveMaintenance: true,
                AttachmentTypes: ["FrontView", "BackView", "SerialOrTag", "Damage", "BeforeRepair", "AfterRepair", "Receipt", "Other"]),
            Statuses:
            [
                new("Open", "#64748b", 1, IsInitial: true),
                new("In Progress", "#7c3aed", 2),
                new("Waiting", "#d97706", 3, IsBlocked: true),
                new("Completed", "#059669", 4, IsFinal: true),
                new("Cancelled", "#dc2626", 5, IsCancelled: true)
            ],
            Fields: [],
            Checklists:
            [
                new("general_condition", "General Intake Visual Inspection", "Pemeriksaan Visual Awal", "General")
            ],
            ServiceTemplates: [])
    ];

    public static IndustryBlueprintDefinition GetByKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return All[0];
        }

        return All.FirstOrDefault(b => string.Equals(b.Key, key.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? All[0];
    }

    public static string SerializeTerminology(TerminologyConfigDto terminology) =>
        JsonSerializer.Serialize(terminology, JsonOptions);

    public static TerminologyConfigDto DeserializeTerminology(string? json, string blueprintKey)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<TerminologyConfigDto>(json, JsonOptions);
                if (parsed?.En is not null && parsed.Id is not null)
                {
                    return parsed;
                }
            }
            catch
            {
                // Fall back to blueprint defaults
            }
        }

        return GetByKey(blueprintKey).Terminology;
    }

    public static WorkspaceFeatureConfig GetDefaultFeatureConfig(string blueprintKey, OperatingMode? operatingMode = null)
    {
        var defaults = GetByKey(blueprintKey).FeatureConfig;
        if (operatingMode == OperatingMode.InternalMaintenance)
        {
            return defaults with
            {
                ShowBillingAndPayments = false,
                ShowCustomerApprovals = false
            };
        }

        if (operatingMode is OperatingMode.Commercial or OperatingMode.Hybrid &&
            string.Equals(blueprintKey?.Trim(), "facility_internal", StringComparison.OrdinalIgnoreCase))
        {
            return defaults with
            {
                ShowBillingAndPayments = true,
                ShowCustomerApprovals = true
            };
        }

        return defaults;
    }

    public static string SerializeFeatureConfig(WorkspaceFeatureConfig config) =>
        JsonSerializer.Serialize(config, JsonOptions);

    public static WorkspaceFeatureConfig DeserializeFeatureConfig(string? json, string blueprintKey, OperatingMode? operatingMode = null)
    {
        var fallback = GetDefaultFeatureConfig(blueprintKey, operatingMode);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<WorkspaceFeatureConfig>(json, JsonOptions);
                if (parsed is not null)
                {
                    return parsed with
                    {
                        QuickAccessoriesEn = parsed.QuickAccessoriesEn ?? fallback.QuickAccessoriesEn,
                        QuickAccessoriesId = parsed.QuickAccessoriesId ?? fallback.QuickAccessoriesId,
                        AttachmentTypes = parsed.AttachmentTypes is { Length: > 0 } ? parsed.AttachmentTypes : fallback.AttachmentTypes
                    };
                }
            }
            catch
            {
                // Fall back to blueprint defaults
            }
        }

        return fallback;
    }
}
