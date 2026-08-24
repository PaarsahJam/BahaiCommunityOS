namespace CommunityOS.Localization.Domain;

/// <summary>
/// Static validation for BCP-47 culture codes (ADR-029 decision 3). Codes are
/// stored canonically lower-cased; the shape check is deliberately pragmatic
/// (language sub-tag plus optional script/region/extension sub-tags) so any
/// well-formed tag can be registered without depending on OS culture tables.
/// </summary>
public static partial class Bcp47
{
    public static bool IsValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var candidate = code.Trim();
        if (candidate.Length is < 2 or > 35)
        {
            return false;
        }

        var subTags = candidate.Split('-');
        if (subTags[0].Length is < 2 or > 8 || !IsAlphabetic(subTags[0]))
        {
            return false;
        }

        foreach (var subTag in subTags.Skip(1))
        {
            if (subTag.Length is < 1 or > 8 || !IsAlphanumeric(subTag))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Canonical storage form: trimmed, lower-cased, invariant.</summary>
    public static string Normalize(string code) => code.Trim().ToLowerInvariant();

    /// <summary>The BCP-47 fallback chain for a code: the code itself followed
    /// by successive parent truncations ("fa-IR" → "fa"). The chain excludes
    /// the terminal default-locale substitution, which resolution applies.</summary>
    public static IReadOnlyList<string> FallbackChain(string culture)
    {
        var normalized = Normalize(culture);
        var chain = new List<string> { normalized };
        while (true)
        {
            var cut = normalized.LastIndexOf('-');
            if (cut <= 0)
            {
                break;
            }

            normalized = normalized[..cut];
            chain.Add(normalized);
        }

        return chain;
    }

    private static bool IsAlphabetic(string value) => value.All(char.IsAsciiLetter);

    private static bool IsAlphanumeric(string value) => value.All(char.IsAsciiLetterOrDigit);
}
