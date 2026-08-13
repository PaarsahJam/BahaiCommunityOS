namespace CommunityOS.Community.Application.DTOs;

public sealed record MembershipDto(
    Guid Id,
    Guid PersonId,
    string Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    DateTime? WithdrawnOn,
    IReadOnlyList<MembershipPeriodDto> History);

public sealed record MembershipPeriodDto(
    string Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil);
