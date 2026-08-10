namespace CommunityOS.Authorization.Application.DTOs;

public sealed record AuthorizationCheckDto(
    string CheckKey,
    bool Allowed,
    string DecisionId,
    string Reason,
    DateTime EvaluatedOn);

public sealed record BatchAuthorizationCheckDto(IReadOnlyList<AuthorizationCheckDto> Results);

public sealed record RoleDto(
    Guid Id,
    string Code,
    string DisplayName,
    string? Description,
    bool Enabled,
    IReadOnlyList<string> Permissions);

public sealed record RoleAssignmentDto(
    Guid Id,
    Guid SubjectId,
    Guid RoleId,
    string RoleCode,
    string ScopeType,
    Guid? ScopeId,
    string? ResourceType,
    Guid GrantedBy,
    DateTime GrantedAt,
    DateTime EffectiveFrom,
    DateTime? EffectiveUntil,
    string? Reason,
    bool IsRevoked,
    Guid? RevokedBy,
    DateTime? RevokedAt);

public sealed record RelationshipDto(
    Guid Id,
    Guid SubjectId,
    string Relation,
    string ObjectType,
    Guid ObjectId,
    IReadOnlyList<string> Permissions);

public sealed record DelegationDto(
    Guid Id,
    Guid DelegatorId,
    Guid DelegateId,
    IReadOnlyList<string> Permissions,
    string ScopeType,
    Guid? ScopeId,
    string? ResourceType,
    DateTime StartsOn,
    DateTime ExpiresOn,
    string? Reason,
    bool IsRevoked);

public sealed record BreakGlassRequestDto(
    Guid Id,
    Guid RequesterId,
    string State,
    IReadOnlyList<string> Permissions,
    string ScopeType,
    Guid? ScopeId,
    string? ResourceType,
    string Reason,
    DateTime RequestedAt,
    int RequestedDurationMinutes,
    Guid? ApproverId,
    DateTime? ApprovedOn,
    DateTime? ApprovedUntil,
    string? RejectionReason,
    Guid? RevokedBy,
    DateTime? RevokedAt,
    string? RevocationReason);
