namespace CommunityOS.Community.Application.DTOs;

public sealed record CommunityEventDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime? EndsAt,
    string TimeZone,
    string? Location,
    bool IsOnline,
    string? OnlineUrl,
    Guid? OrganizerPersonId,
    Guid? OrganizationUnitId,
    string Status,
    string Visibility,
    bool RegistrationOpen,
    int? Capacity,
    DateTime CreatedOn);
