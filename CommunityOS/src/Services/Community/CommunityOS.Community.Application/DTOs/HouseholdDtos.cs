namespace CommunityOS.Community.Application.DTOs;

public sealed record HouseholdDto(
    Guid Id,
    string? Name,
    string? Line1,
    string? Line2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    IReadOnlyList<HouseholdMemberDto> Members);

public sealed record HouseholdMemberDto(
    Guid Id,
    Guid PersonId,
    string Role,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil);

public sealed record FamilyRelationshipDto(
    Guid Id,
    Guid PersonIdA,
    Guid PersonIdB,
    string RelationshipType,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil);
