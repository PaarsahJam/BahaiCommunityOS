namespace CommunityOS.Community.Application.DTOs;

/// <summary>
/// Basic person profile. Contact methods and sensitive attributes are only
/// included in <see cref="PersonDetailDto"/> when the caller holds the
/// appropriate privacy-scoped permission.
/// </summary>
public sealed record PersonDto(
    Guid Id,
    string PreferredName,
    string? FormalName,
    string? PreferredLanguage,
    string Status,
    bool HasLinkedIdentityAccount,
    DateTime CreatedOn);

/// <summary>
/// Full person detail. <see cref="ContactMethods"/> and
/// <see cref="DateOfBirth"/> are populated conditionally based on the caller's
/// <c>community.person.contact.read</c> / <c>community.person.sensitive.read</c>
/// grants; otherwise they are returned as <c>null</c>/empty.
/// </summary>
public sealed record PersonDetailDto(
    Guid Id,
    string PreferredName,
    string? FormalName,
    DateTime? DateOfBirth,
    string? PreferredLanguage,
    string Status,
    Guid? IdentityAccountId,
    string ProfileVisibility,
    string ContactVisibility,
    string DateOfBirthVisibility,
    IReadOnlyList<ContactMethodDto> ContactMethods,
    DateTime CreatedOn);

public sealed record ContactMethodDto(
    Guid Id,
    string Type,
    string Value,
    bool IsPreferred,
    string Visibility);
