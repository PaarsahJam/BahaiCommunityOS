namespace CommunityOS.Community.Application.DTOs;

public sealed record ParticipationDto(
    Guid Id,
    Guid PersonId,
    string TargetType,
    Guid TargetId,
    string? Role,
    string Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    DateTime RecordedOn);
