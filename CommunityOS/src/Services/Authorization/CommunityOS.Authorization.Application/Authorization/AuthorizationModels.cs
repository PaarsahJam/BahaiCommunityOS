using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.ValueObjects;

namespace CommunityOS.Authorization.Application.Authorization;

/// <summary>
/// Contextual attributes of an authorization decision: where (organization
/// unit) and on what (resource) the action is being evaluated, plus optional
/// attribute-based conditions (e.g. workflow state, classification,
/// authentication assurance). The client never supplies roles, permissions or
/// authorization decisions — only context.
/// </summary>
public sealed record AuthorizationContext(
    Guid? OrganizationUnitId = null,
    string? ResourceType = null,
    Guid? ResourceId = null,
    IReadOnlyDictionary<string, string>? Attributes = null)
{
    public static readonly AuthorizationContext Empty = new();

    public bool IsGlobal => OrganizationUnitId is null && ResourceId is null;
}

/// <summary>
/// A single "may subject perform permission on resource in context" question.
/// </summary>
public sealed record AuthorizationRequest(
    Guid SubjectId,
    string Permission,
    AuthorizationContext Context)
{
    public AuthorizationRequest(Guid subjectId, string permission)
        : this(subjectId, permission, AuthorizationContext.Empty)
    {
    }
}

/// <summary>
/// Outcome of an authorization decision. The decision is fail-closed: when a
/// decision cannot be established safely it is a Deny.
/// </summary>
public sealed record AuthorizationDecision(
    bool Allowed,
    AuthorizationDecisionReason Reason,
    string? PolicyReference,
    IReadOnlyList<string> PolicyReferences,
    DateTime EvaluatedOn,
    string DecisionId)
{
    public static AuthorizationDecision Allow(
        string decisionId,
        IReadOnlyList<string> policyReferences,
        DateTime evaluatedOn) =>
        new(true, AuthorizationDecisionReason.Allowed,
            policyReferences.Count != 0 ? policyReferences[0] : null,
            policyReferences, evaluatedOn, decisionId);

    public static AuthorizationDecision Deny(
        string decisionId,
        AuthorizationDecisionReason reason,
        DateTime evaluatedOn) =>
        new(false, reason, null, [], evaluatedOn, decisionId);
}

/// <summary>
/// Coarse, non-sensitive classification of a decision outcome. Full policy
/// internals are never exposed to clients.
/// </summary>
public enum AuthorizationDecisionReason
{
    Allowed,
    DeniedByDefault,
    MissingSubject,
    InvalidPermission,
    NoPermission,
    ScopeMismatch
}
