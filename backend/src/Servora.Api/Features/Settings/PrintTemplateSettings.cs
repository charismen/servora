namespace Servora.Api.Features.Settings;

public sealed record PrintTemplateSettings(
    string TermsAndConditions,
    bool ShowPhysicalCondition,
    bool ShowAccessories,
    bool ShowPhotoNotes,
    bool ShowQrCode,
    bool ShowPhoneNumber,
    bool ShowPaymentSummary = true);

public static class PrintTemplateSettingKeys
{
    public const string TermsAndConditions = "PrintTemplate.TermsAndConditions";
    public const string ShowPhysicalCondition = "PrintTemplate.ShowPhysicalCondition";
    public const string ShowAccessories = "PrintTemplate.ShowAccessories";
    public const string ShowPhotoNotes = "PrintTemplate.ShowPhotoNotes";
    public const string ShowQrCode = "PrintTemplate.ShowQrCode";
    public const string ShowPhoneNumber = "PrintTemplate.ShowPhoneNumber";
    public const string ShowPaymentSummary = "PrintTemplate.ShowPaymentSummary";

    public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        [TermsAndConditions] = "Please bring this receipt when collecting your device. Service warranty follows the written warranty terms on the ticket.",
        [ShowPhysicalCondition] = "true",
        [ShowAccessories] = "true",
        [ShowPhotoNotes] = "true",
        [ShowQrCode] = "true",
        [ShowPhoneNumber] = "true",
        [ShowPaymentSummary] = "true"
    };
}
