using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.ValueObjects;

public sealed class Email : ValueObject
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        Guard.NotNullOrWhiteSpace(value, nameof(value));
        var normalised = value.Trim().ToLowerInvariant();
        if (!normalised.Contains('@') || normalised.Length > 254)
            throw new ArgumentException("Invalid email address.", nameof(value));
        return new Email(normalised);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
