using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.ValueObjects;

/// <summary>
/// A person's privacy preferences. Privacy is a Community concern; exposure of
/// personal data is further gated by application-layer permissions
/// (e.g. <c>community.person.contact.read</c>, <c>community.person.sensitive.read</c>).
/// </summary>
public sealed class PrivacyPreferences : ValueObject
{
    private PrivacyPreferences(
        ContactVisibility profileVisibility,
        ContactVisibility contactVisibility,
        ContactVisibility dateOfBirthVisibility)
    {
        ProfileVisibility = profileVisibility;
        ContactVisibility = contactVisibility;
        DateOfBirthVisibility = dateOfBirthVisibility;
    }

    public ContactVisibility ProfileVisibility { get; }
    public ContactVisibility ContactVisibility { get; }
    public ContactVisibility DateOfBirthVisibility { get; }

    public static PrivacyPreferences Create(
        ContactVisibility profileVisibility,
        ContactVisibility contactVisibility,
        ContactVisibility dateOfBirthVisibility)
    {
        Guard.NotNull(profileVisibility, nameof(profileVisibility));
        Guard.NotNull(contactVisibility, nameof(contactVisibility));
        Guard.NotNull(dateOfBirthVisibility, nameof(dateOfBirthVisibility));

        return new PrivacyPreferences(profileVisibility, contactVisibility, dateOfBirthVisibility);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProfileVisibility;
        yield return ContactVisibility;
        yield return DateOfBirthVisibility;
    }
}
