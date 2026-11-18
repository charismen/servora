namespace Servora.Api.Features.Settings;

public static class ShopTimeZoneHelper
{
    private static string _defaultTimeZoneId = "Asia/Jakarta";

    public static string DefaultTimeZoneId
    {
        get => _defaultTimeZoneId;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _defaultTimeZoneId = value.Trim();
            }
        }
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            timeZoneId = DefaultTimeZoneId;
        }

        var normalized = timeZoneId.Trim();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(normalized);
        }
        catch
        {
            // Windows <-> IANA alias fallbacks
            if (normalized.Equals("Asia/Jakarta", StringComparison.OrdinalIgnoreCase))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); } catch { }
            }
            else if (normalized.Equals("SE Asia Standard Time", StringComparison.OrdinalIgnoreCase))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta"); } catch { }
            }
            else if (normalized.Equals("Asia/Singapore", StringComparison.OrdinalIgnoreCase))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); } catch { }
            }
            else if (normalized.Equals("Singapore Standard Time", StringComparison.OrdinalIgnoreCase))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore"); } catch { }
            }
            else if (normalized.Equals("Asia/Makassar", StringComparison.OrdinalIgnoreCase))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); } catch { }
            }
            else if (normalized.Equals("Asia/Jayapura", StringComparison.OrdinalIgnoreCase))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"); } catch { }
            }
            else if (normalized.Equals("UTC", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Etc/UTC", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneInfo.Utc;
            }

            return TimeZoneInfo.Utc;
        }
    }

    public static DateTimeOffset CreateShopDateTimeOffset(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var localDateTime = date.ToDateTime(time);
        var offset = timeZone.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset);
    }
}
