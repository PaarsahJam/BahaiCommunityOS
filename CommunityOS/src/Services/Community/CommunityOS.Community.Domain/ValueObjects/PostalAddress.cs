using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.ValueObjects;

/// <summary>
/// A postal address. Households and contact methods may carry an address;
/// family members are not required to share one.
/// </summary>
public sealed class PostalAddress : ValueObject
{
    private PostalAddress(string? line1, string? line2, string? city, string? region, string? postalCode, string country)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        Region = region;
        PostalCode = postalCode;
        Country = country;
    }

    public string? Line1 { get; }
    public string? Line2 { get; }
    public string? City { get; }
    public string? Region { get; }
    public string? PostalCode { get; }
    public string Country { get; }

    public static PostalAddress Create(
        string? line1,
        string? line2,
        string? city,
        string? region,
        string? postalCode,
        string country)
    {
        Guard.NotNullOrWhiteSpace(country, nameof(country));
        Guard.MaxLength(country, 100, nameof(country));
        Guard.MaxLength(line1 ?? string.Empty, 200, nameof(line1));
        Guard.MaxLength(line2 ?? string.Empty, 200, nameof(line2));
        Guard.MaxLength(city ?? string.Empty, 100, nameof(city));
        Guard.MaxLength(region ?? string.Empty, 100, nameof(region));
        Guard.MaxLength(postalCode ?? string.Empty, 20, nameof(postalCode));

        return new PostalAddress(
            NullIfBlank(line1), NullIfBlank(line2), NullIfBlank(city),
            NullIfBlank(region), NullIfBlank(postalCode), country.Trim());
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Line1;
        yield return Line2;
        yield return City;
        yield return Region;
        yield return PostalCode;
        yield return Country;
    }
}
