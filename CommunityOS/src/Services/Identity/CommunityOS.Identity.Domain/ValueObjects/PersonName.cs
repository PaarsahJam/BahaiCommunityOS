using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.ValueObjects;

public sealed class PersonName : ValueObject
{
    public string FirstName { get; }
    public string LastName { get; }
    public string? MiddleName { get; }

    private PersonName(string firstName, string lastName, string? middleName)
    {
        FirstName = firstName;
        LastName = lastName;
        MiddleName = middleName;
    }

    public static PersonName Create(string firstName, string lastName, string? middleName = null)
    {
        Guard.NotNullOrWhiteSpace(firstName, nameof(firstName));
        Guard.NotNullOrWhiteSpace(lastName, nameof(lastName));
        Guard.MaxLength(firstName, 100, nameof(firstName));
        Guard.MaxLength(lastName, 100, nameof(lastName));
        return new PersonName(firstName.Trim(), lastName.Trim(), middleName?.Trim());
    }

    public string FullName =>
        MiddleName is null ? $"{FirstName} {LastName}" : $"{FirstName} {MiddleName} {LastName}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FirstName;
        yield return MiddleName;
        yield return LastName;
    }

    public override string ToString() => FullName;
}
