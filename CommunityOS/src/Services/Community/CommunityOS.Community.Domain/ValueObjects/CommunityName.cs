using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.ValueObjects;

public sealed class CommunityName : ValueObject
{
    public string Value { get; }

    private CommunityName(string value) => Value = value;

    public static CommunityName Create(string value)
    {
        Guard.NotNullOrWhiteSpace(value, nameof(value));
        Guard.MaxLength(value, 200, nameof(value));
        return new CommunityName(value.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
