using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Finance.Domain.ValueObjects;

/// <summary>
/// A monetary magnitude stored in minor units (ADR-032). Amounts are always
/// positive; the sign is carried by the entry's direction so the ledger stays
/// append-only and a correction appends an opposite entry rather than editing
/// or negating a row. Identity is (currency + minor units).
/// </summary>
public sealed class Money : ValueObject
{
    private Money(string currency, long minorUnits)
    {
        Currency = currency;
        MinorUnits = minorUnits;
    }

    /// <summary>Uppercase three-letter ISO-4217 code.</summary>
    public string Currency { get; }

    /// <summary>Positive magnitude in minor units (e.g. cents).</summary>
    public long MinorUnits { get; }

    public static Money Create(string currency, long minorUnits)
    {
        if (minorUnits <= 0)
            throw new InvalidFinancialAmountException(minorUnits);

        if (!FinanceConstants.IsValidCurrencyCode(currency))
            throw new InvalidCurrencyCodeException(currency);

        return new Money(currency.ToUpperInvariant(), minorUnits);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Currency;
        yield return MinorUnits;
    }
}