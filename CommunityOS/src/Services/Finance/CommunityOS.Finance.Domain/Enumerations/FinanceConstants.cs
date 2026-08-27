namespace CommunityOS.Finance.Domain.Enumerations;

/// <summary>
/// Canonical Finance domain constants (ADR-032).
/// </summary>
public static class FinanceConstants
{
    /// <summary>
    /// True when <paramref name="currency"/> is a three-letter ISO-4217 code
    /// in uppercase (e.g. <c>USD</c>, <c>EUR</c>, <c>IRR</c>). Currencies are
    /// stored and compared case-insensitively as uppercase codes only.
    /// </summary>
    public static bool IsValidCurrencyCode(string? currency) =>
        currency is not null &&
        currency.Length == 3 &&
        currency.All(c => c is >= 'A' and <= 'Z');
}