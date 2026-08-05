using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.ValueObjects;

public sealed class GeographicArea : ValueObject
{
    public string Country { get; }
    public string? Region { get; }
    public string? City { get; }
    public double? Latitude { get; }
    public double? Longitude { get; }

    private GeographicArea(string country, string? region, string? city,
        double? latitude, double? longitude)
    {
        Country = country;
        Region = region;
        City = city;
        Latitude = latitude;
        Longitude = longitude;
    }

    public static GeographicArea Create(string country, string? region = null,
        string? city = null, double? latitude = null, double? longitude = null)
    {
        Guard.NotNullOrWhiteSpace(country, nameof(country));
        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude));
        return new GeographicArea(country.Trim(), region?.Trim(), city?.Trim(), latitude, longitude);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Country;
        yield return Region;
        yield return City;
        yield return Latitude;
        yield return Longitude;
    }
}
