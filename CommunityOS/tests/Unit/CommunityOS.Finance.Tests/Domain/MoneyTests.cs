using CommunityOS.Finance.Domain.Exceptions;
using CommunityOS.Finance.Domain.ValueObjects;

namespace CommunityOS.Finance.Tests.Domain;

/// <summary>
/// Locks the monetary magnitude rules (ADR-032): amounts are always positive
/// minor units with an uppercase ISO-4217 code; the sign is carried by the
/// entry direction, never by the amount.
/// </summary>
public class MoneyTests
{
    [Fact]
    public void Create_accepts_an_uppercase_code_and_keeps_minor_units()
    {
        var money = Money.Create("EUR", 1234);

        money.Currency.Should().Be("EUR");
        money.MinorUnits.Should().Be(1234);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_rejects_non_positive_minor_units(long minorUnits)
    {
        var act = () => Money.Create("USD", minorUnits);

        act.Should().Throw<InvalidFinancialAmountException>();
    }

    [Theory]
    [InlineData("us")]
    [InlineData("USDD")]
    [InlineData("usd")]
    [InlineData("U1D")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_rejects_a_non_uppercase_three_letter_currency(string? currency)
    {
        var act = () => Money.Create(currency!, 100);

        act.Should().Throw<InvalidCurrencyCodeException>();
    }

    [Fact]
    public void Equality_is_based_on_currency_and_minor_units()
    {
        var a = Money.Create("USD", 100);
        var b = Money.Create("USD", 100);
        var c = Money.Create("EUR", 100);

        a.Should().Be(b);
        a.Should().NotBe(c);
        a.Equals(c).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}