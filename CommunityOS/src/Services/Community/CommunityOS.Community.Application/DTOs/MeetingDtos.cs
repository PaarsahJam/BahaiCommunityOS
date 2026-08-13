namespace CommunityOS.Community.Application.DTOs;

public sealed record MeetingDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime? EndsAt,
    string TimeZone,
    string? Location,
    Guid? OrganizerPersonId,
    Guid? OrganizationUnitId,
    string Status,
    string Visibility,
    string? Minutes,
    IReadOnlyList<MeetingParticipantDto> Participants,
    IReadOnlyList<MeetingAgendaItemDto> AgendaItems,
    IReadOnlyList<MeetingActionDto> Actions,
    DateTime CreatedOn);

public sealed record MeetingParticipantDto(
    Guid Id,
    Guid PersonId,
    string Role,
    string Attendance);

public sealed record MeetingAgendaItemDto(
    Guid Id,
    string Title,
    string? Description,
    int Order,
    bool IsCompleted);

public sealed record MeetingActionDto(
    Guid Id,
    string Description,
    Guid? AssigneePersonId,
    DateTime? DueDate,
    bool IsCompleted,
    DateTime? CompletedOn);
