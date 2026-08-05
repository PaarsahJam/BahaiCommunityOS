using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.ValueObjects;

public sealed class PhoneNumber : ValueObject
{
    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber Create(string value)
    {
        Guard.NotNullOrWhiteSpace(value, nameof(value));
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length < 7 || digits.Length > 15)
            throw new ArgumentException("Invalid phone number.", nameof(value));
        return new PhoneNumber(value.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
