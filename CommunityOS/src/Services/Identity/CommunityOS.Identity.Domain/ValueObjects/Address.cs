using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string Line1 { get; }
    public string? Line2 { get; }
    public string City { get; }
    public string? StateProvince { get; }
    public string PostalCode { get; }
    public string CountryCode { get; }

    private Address(string line1, string? line2, string city,
        string? stateProvince, string postalCode, string countryCode)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        StateProvince = stateProvince;
        PostalCode = postalCode;
        CountryCode = countryCode;
    }

    public static Address Create(string line1, string city, string postalCode, string countryCode,
        string? line2 = null, string? stateProvince = null)
    {
        Guard.NotNullOrWhiteSpace(line1, nameof(line1));
        Guard.NotNullOrWhiteSpace(city, nameof(city));
        Guard.NotNullOrWhiteSpace(postalCode, nameof(postalCode));
        Guard.NotNullOrWhiteSpace(countryCode, nameof(countryCode));
        if (countryCode.Length != 2)
            throw new ArgumentException("Country code must be ISO 3166-1 alpha-2.", nameof(countryCode));
        return new Address(line1.Trim(), line2?.Trim(), city.Trim(),
            stateProvince?.Trim(), postalCode.Trim(), countryCode.ToUpperInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Line1;
        yield return Line2;
        yield return City;
        yield return StateProvince;
        yield return PostalCode;
        yield return CountryCode;
    }
}
