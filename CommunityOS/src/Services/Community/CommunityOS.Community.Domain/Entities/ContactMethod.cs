using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;
using System.Text.RegularExpressions;

namespace CommunityOS.Community.Domain.Entities;

/// <summary>
/// A single contact method (email, phone or postal) on a person record.
/// Values are structurally validated; duplicate values are rejected by the
/// person aggregate.
/// </summary>
public sealed class ContactMethod : Entity<Guid>
{
    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private ContactMethod() : base(Guid.Empty)
    {
        Value = null!;
        Type = null!;
        Visibility = null!;
    }

    private ContactMethod(
        Guid id,
        ContactMethodType type,
        string value,
        bool isPreferred,
        ContactVisibility visibility) : base(id)
    {
        Type = type;
        Value = value;
        IsPreferred = isPreferred;
        Visibility = visibility;
    }

    public ContactMethodType Type { get; private set; }
    public string Value { get; private set; }
    public bool IsPreferred { get; private set; }
    public ContactVisibility Visibility { get; private set; }

    public static ContactMethod Create(
        ContactMethodType type,
        string value,
        bool isPreferred,
        ContactVisibility visibility)
    {
        Guard.NotNull(type, nameof(type));
        Guard.NotNullOrWhiteSpace(value, nameof(value));
        Guard.MaxLength(value, 320, nameof(value));
        Guard.NotNull(visibility, nameof(visibility));

        var normalized = value.Trim();
        if (type == ContactMethodType.Email && !EmailPattern.IsMatch(normalized))
            throw new InvalidContactMethodException(type.Name, normalized);

        return new ContactMethod(Guid.NewGuid(), type, normalized, isPreferred, visibility);
    }

    public void MarkPreferred() => IsPreferred = true;

    public void ClearPreferred() => IsPreferred = false;

    public override string ToString() => $"{Type.Name}: {Value}";
}
