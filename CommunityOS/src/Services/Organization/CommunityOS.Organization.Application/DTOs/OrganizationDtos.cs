namespace CommunityOS.Organization.Application.DTOs;

public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string OrganizationType,
    string Status,
    string JurisdictionType,
    Guid? JurisdictionScopeId,
    DateTime? EstablishedOn,
    DateTime? DissolvedOn,
    DateTime CreatedOn);

public sealed record OrganizationUnitDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string UnitType,
    bool IsActive,
    Guid? ParentId,
    DateTime CreatedOn);

public sealed record OrganizationUnitParentDto(
    Guid? ParentId,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil);

public sealed record OrganizationUnitDetailDto(
    OrganizationUnitDto Unit,
    IReadOnlyList<OrganizationUnitParentDto> ParentHistory,
    IReadOnlyList<OrganizationUnitDto> Children);

public sealed record AppointmentDto(
    Guid Id,
    Guid PersonId,
    Guid OrganizationUnitId,
    string AppointmentType,
    string Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    Guid? AssignedBy,
    DateTime AssignedOn,
    Guid? EndedBy,
    DateTime? EndedOn,
    string? Reason);

public sealed record CommitteeMemberDto(
    Guid PersonId,
    string RoleCode,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil);

public sealed record CommitteeDto(
    Guid Id,
    string Name,
    string CommitteeType,
    Guid OrganizationId,
    Guid? OrganizationUnitId,
    string JurisdictionType,
    Guid? JurisdictionScopeId,
    bool IsActive,
    DateTime CreatedOn,
    IReadOnlyList<CommitteeMemberDto> Members);

public sealed record InstitutionDto(
    Guid Id,
    string Name,
    string InstitutionType,
    string JurisdictionType,
    Guid? JurisdictionScopeId,
    bool IsActive,
    DateTime? EstablishedOn,
    DateTime CreatedOn);

public sealed record DelegationFactDto(
    Guid Id,
    Guid DelegatorId,
    Guid DelegateId,
    Guid OrganizationUnitId,
    string DelegationType,
    string Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    Guid GrantedBy,
    DateTime GrantedOn,
    Guid? RevokedBy,
    DateTime? RevokedOn,
    string? Reason);

/// <summary>
/// Answer to the internal "is candidate an ancestor-or-self of unit" fact query
/// used by the Authorization service to resolve organization-scoped grants.
/// </summary>
public sealed record OrganizationUnitCoverageDto(
    Guid OrganizationUnitId,
    Guid CandidateAncestorId,
    bool Covers,
    DateTime EvaluatedOn);
