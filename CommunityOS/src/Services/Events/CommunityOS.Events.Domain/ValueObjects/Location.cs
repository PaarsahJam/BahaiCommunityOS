using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Events.Domain.ValueObjects;

public sealed class Location : ValueObject
{
    public string Name { get; }
    public string? Address { get; }
    public bool IsVirtual { get; }
    public string? VirtualLink { get; }

    private Location(string name, string? address, bool isVirtual, string? virtualLink)
    {
        Name = name;
        Address = address;
        IsVirtual = isVirtual;
        VirtualLink = virtualLink;
    }

    public static Location CreatePhysical(string name, string address)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.NotNullOrWhiteSpace(address, nameof(address));
        return new Location(name.Trim(), address.Trim(), false, null);
    }

    public static Location CreateVirtual(string name, string virtualLink)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.NotNullOrWhiteSpace(virtualLink, nameof(virtualLink));
        return new Location(name.Trim(), null, true, virtualLink.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Address;
        yield return IsVirtual;
        yield return VirtualLink;
    }
}
