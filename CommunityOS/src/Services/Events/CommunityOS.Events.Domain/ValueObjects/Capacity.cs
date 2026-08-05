using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Events.Domain.ValueObjects;

public sealed class Capacity : ValueObject
{
    public int? Maximum { get; }
    public bool IsUnlimited => Maximum is null;

    private Capacity(int? maximum) => Maximum = maximum;

    public static Capacity Unlimited() => new(null);

    public static Capacity Limited(int maximum)
    {
        Guard.Positive(maximum, nameof(maximum));
        return new Capacity(maximum);
    }

    public bool CanAccommodate(int count) =>
        IsUnlimited || count <= Maximum!.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Maximum;
    }
}
