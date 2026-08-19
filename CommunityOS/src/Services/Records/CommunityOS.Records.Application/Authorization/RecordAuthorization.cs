using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Records.Domain.Aggregates;

namespace CommunityOS.Records.Application.Authorization;

/// <summary>
/// Builds the resource-level authorization contexts for a record (ADR-023).
/// A record may belong to multiple organization scopes; access is granted when
/// the caller holds the permission at <b>any</b> of the record's scopes (primary
/// or additional). Every check passes <c>resourceType = "record"</c> and the
/// record id so per-record grants (relationship tuples) and organization-scoped
/// grants compose exactly as in other services. Fail-closed: any inability to
/// establish the grant is a Deny.
/// </summary>
internal static class RecordAuthorization
{
    /// <summary>
    /// One context per organization-unit scope (primary plus additional scopes),
    /// or a single global resource context when the record has no unit scope.
    /// </summary>
    public static IReadOnlyList<AuthorizationContext> ContextsFor(Record record)
    {
        var units = record.AllOrganizationUnitIds.ToList();
        if (units.Count == 0)
            return [new AuthorizationContext(ResourceType: "record", ResourceId: record.Id)];

        return units
            .Select(unit => new AuthorizationContext(unit, "record", record.Id))
            .ToList();
    }

    /// <summary>
    /// True when the caller holds <paramref name="permission"/> at any of the
    /// record's organization scopes (or at the global resource scope when the
    /// record has no unit scope).
    /// </summary>
    public static async Task<bool> HasForRecordAsync(
        AuthorizationGuard guard,
        Guid actorId,
        string permission,
        Record record,
        CancellationToken ct)
    {
        foreach (var context in ContextsFor(record))
        {
            if (await guard.HasAsync(actorId, permission, context, ct))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Throws <see cref="AuthorizationForbiddenException"/> when the caller does
    /// not hold <paramref name="permission"/> at any of the record's scopes.
    /// </summary>
    public static async Task RequireForRecordAsync(
        AuthorizationGuard guard,
        Guid actorId,
        string permission,
        Record record,
        CancellationToken ct)
    {
        if (!await HasForRecordAsync(guard, actorId, permission, record, ct))
            throw new AuthorizationForbiddenException(permission);
    }
}