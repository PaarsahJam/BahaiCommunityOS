namespace CommunityOS.Community.Application.DTOs;

public sealed record ActivityDto(
    Guid Id,
    string Title,
    string? Description,
    string? Category,
    Guid? OrganizerPersonId,
    Guid? OrganizationUnitId,
    string? Location,
    bool IsOnline,
    string? OnlineUrl,
    DateTime StartsAt,
    DateTime? EndsAt,
    string Visibility,
    string Status,
    int? Capacity,
    DateTime CreatedOn);
