namespace Servora.Api.Features.Settings;

public sealed record ShopProfileSettings(
    string ShopName,
    string PhoneNumber,
    string WhatsAppNumber,
    string Email,
    string Address,
    string TicketPrefix,
    string TicketDateFormat,
    int TicketStartNumber,
    int TicketMinDigits,
    string CurrencyCode,
    string ReceiptFooter,
    decimal InventoryDefaultTaxPercent,
    decimal InventoryRoundingIncrement,
    string InventoryDefaultUnit,
    bool InventoryAllowDecimalQuantity,
    bool InventoryAutoCreateCategoryOnImport,
    bool RequirePhotoEvidenceOnIntake,
    bool ShowPhotoEvidenceOnPublicTracking,
    bool RequireApprovalEstimate,
    bool AllowUnpaidPickupOverride,
    string OperationalOpenTime,
    string OperationalCloseTime,
    int OperationalRequiredWorkingDaysPerWeek,
    int OperationalRequiredWorkingDaysPerMonth,
    int OperationalDefaultBreakMinutes,
    string OperationalTimeZone,
    string? LogoUrl,
    DateTimeOffset? UpdatedAt = null,
    string InventoryDefaultTaxMode = "Percentage");

public static class ShopProfileSettingKeys
{
    public const string ShopName = "ShopProfile.ShopName";
    public const string PhoneNumber = "ShopProfile.PhoneNumber";
    public const string WhatsAppNumber = "ShopProfile.WhatsAppNumber";
    public const string Email = "ShopProfile.Email";
    public const string Address = "ShopProfile.Address";
    public const string TicketPrefix = "ShopProfile.TicketPrefix";
    public const string TicketDateFormat = "TicketNumbering.DateFormat";
    public const string TicketStartNumber = "TicketNumbering.StartNumber";
    public const string TicketMinDigits = "TicketNumbering.MinDigits";
    public const string CurrencyCode = "ShopProfile.CurrencyCode";
    public const string ReceiptFooter = "ShopProfile.ReceiptFooter";
    public const string InventoryDefaultTaxPercent = "Inventory.DefaultTaxPercent";
    public const string InventoryDefaultTaxMode = "Inventory.DefaultTaxMode";
    public const string InventoryRoundingIncrement = "Inventory.RoundingIncrement";
    public const string InventoryDefaultUnit = "Inventory.DefaultUnit";
    public const string InventoryAllowDecimalQuantity = "Inventory.AllowDecimalQuantity";
    public const string InventoryAutoCreateCategoryOnImport = "Inventory.AutoCreateCategoryOnImport";
    public const string RequirePhotoEvidenceOnIntake = "PhotoEvidence.RequireOnIntake";
    public const string ShowPhotoEvidenceOnPublicTracking = "PublicTracking.ShowPhotoEvidence";
    public const string RequireApprovalEstimate = "Approval.RequireEstimate";
    public const string AllowUnpaidPickupOverride = "Payment.AllowUnpaidPickupOverride";
    public const string OperationalOpenTime = "Operations.OpenTime";
    public const string OperationalCloseTime = "Operations.CloseTime";
    public const string OperationalRequiredWorkingDaysPerWeek = "Operations.RequiredWorkingDaysPerWeek";
    public const string OperationalRequiredWorkingDaysPerMonth = "Operations.RequiredWorkingDaysPerMonth";
    public const string OperationalDefaultBreakMinutes = "Operations.DefaultBreakMinutes";
    public const string OperationalTimeZone = "Operations.TimeZone";

    private static readonly Dictionary<string, string> _defaults = new()
    {
        [ShopName] = "Servora Operations",
        [PhoneNumber] = "+62 812-0000-0000",
        [WhatsAppNumber] = "+62 812-0000-0000",
        [Email] = "admin@example.com",
        [Address] = "Workspace address",
        [TicketPrefix] = "SRV",
        [TicketDateFormat] = "yyyyMMdd",
        [TicketStartNumber] = "1",
        [TicketMinDigits] = "4",
        [CurrencyCode] = "IDR",
        [ReceiptFooter] = "Thank you. Please present this receipt or reference number for pickup or verification.",
        [InventoryDefaultTaxPercent] = "11",
        [InventoryDefaultTaxMode] = "Percentage",
        [InventoryRoundingIncrement] = "1000",
        [InventoryDefaultUnit] = "pcs",
        [InventoryAllowDecimalQuantity] = "true",
        [InventoryAutoCreateCategoryOnImport] = "false",
        [RequirePhotoEvidenceOnIntake] = "false",
        [ShowPhotoEvidenceOnPublicTracking] = "false",
        [RequireApprovalEstimate] = "false",
        [AllowUnpaidPickupOverride] = "true",
        [OperationalOpenTime] = "09:00",
        [OperationalCloseTime] = "18:00",
        [OperationalRequiredWorkingDaysPerWeek] = "6",
        [OperationalRequiredWorkingDaysPerMonth] = "26",
        [OperationalDefaultBreakMinutes] = "60",
        [OperationalTimeZone] = ShopTimeZoneHelper.DefaultTimeZoneId
    };

    public static IReadOnlyDictionary<string, string> Defaults => _defaults;

    public static void SetDefaultTimeZone(string timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            _defaults[OperationalTimeZone] = timeZoneId.Trim();
        }
    }
}
