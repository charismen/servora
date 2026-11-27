using System.Text.RegularExpressions;

namespace Servora.Api.Features.Inventory;

public static partial class InventorySkuGenerator
{
    public const string DefaultPrefix = "ITM";

    /// <summary>
    /// Builds a short 2–4 character uppercase alphanumeric prefix from an optional category name.
    /// Multi-word names use initials (e.g. "Power Supply" -> "PS", "Solid State Drive" -> "SSD");
    /// single-word names use the first 3 alphanumeric characters (e.g. "Battery" -> "BAT", "RAM" -> "RAM").
    /// </summary>
    public static string BuildPrefix(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return DefaultPrefix;

        var words = WordRegex().Matches(categoryName)
            .Select(match => match.Value)
            .Where(word => word.Length > 0)
            .ToArray();

        if (words.Length == 0) return DefaultPrefix;

        if (words.Length >= 2)
        {
            var initials = string.Concat(words.Take(4).Select(word => char.ToUpperInvariant(word[0])));
            if (initials.Length >= 2) return initials;
        }

        var single = words[0].ToUpperInvariant();
        if (single.Length <= 4 && words.Length == 1)
        {
            return single.Length >= 2 ? single : single.PadRight(3, 'X');
        }

        var prefix = single.Length >= 3 ? single[..3] : single;
        return prefix.Length >= 2 ? prefix : DefaultPrefix;
    }

    /// <summary>
    /// Finds the next available SKU matching "{prefix}-{NNN}" (3-digit zero-padded minimum),
    /// scanning existing SKUs case-insensitively and skipping any collisions.
    /// </summary>
    public static (string Sku, string Prefix, int Sequence) BuildNextSku(string prefix, IEnumerable<string> existingSkus)
    {
        var normalizedPrefix = NormalizePrefix(prefix);
        var existingSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var maxSeq = 0;
        var head = normalizedPrefix + "-";

        foreach (var raw in existingSkus)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var sku = raw.Trim();
            existingSet.Add(sku);

            if (!sku.StartsWith(head, StringComparison.OrdinalIgnoreCase)) continue;
            var suffix = sku[head.Length..];
            if (suffix.Length > 0 && suffix.All(char.IsAsciiDigit) && int.TryParse(suffix, out var parsed) && parsed > maxSeq)
            {
                maxSeq = parsed;
            }
        }

        var nextSeq = maxSeq + 1;
        string candidate;
        do
        {
            candidate = $"{normalizedPrefix}-{nextSeq:D3}";
            if (!existingSet.Contains(candidate)) break;
            nextSeq++;
        } while (true);

        return (candidate, normalizedPrefix, nextSeq);
    }

    private static string NormalizePrefix(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DefaultPrefix;
        var cleaned = new string(raw.Trim().ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        if (cleaned.Length < 2) return DefaultPrefix;
        return cleaned.Length > 6 ? cleaned[..6] : cleaned;
    }

    [GeneratedRegex("[A-Za-z0-9]+")]
    private static partial Regex WordRegex();
}
