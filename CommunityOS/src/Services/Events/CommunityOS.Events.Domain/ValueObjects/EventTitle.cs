using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Events.Domain.ValueObjects;

public sealed class EventTitle : ValueObject
{
    public string Value { get; }

    private EventTitle(string value) => Value = value;

    public static EventTitle Create(string value)
    {
        Guard.NotNullOrWhiteSpace(value, nameof(value));
        Guard.MaxLength(value, 250, nameof(value));
        return new EventTitle(value.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
