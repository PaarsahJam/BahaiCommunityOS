namespace CommunityOS.Community.Application.DTOs;

/// <summary>
/// A single calendar entry projected from a community activity, event or
/// meeting. Calendar is a Community read model; no separate calendar service
/// exists yet.
/// </summary>
public sealed record CalendarEntryDto(
    Guid Id,
    string Type,
    string Title,
    DateTime StartsAt,
    DateTime? EndsAt,
    string? TimeZone,
    Guid? OrganizationUnitId,
    string Visibility,
    string Status);
