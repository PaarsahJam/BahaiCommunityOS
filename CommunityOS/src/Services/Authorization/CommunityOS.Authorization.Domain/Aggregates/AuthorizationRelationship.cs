using CommunityOS.Authorization.Domain.Events;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Permissions;
using CommunityOS.Authorization.Domain.Relationships;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Authorization.Domain.Aggregates;

/// <summary>
/// A relationship tuple used for relationship-based access rules, e.g.
/// <c>user belongs_to household</c>, <c>user serves_on committee</c>, or
/// <c>user has_permission records.record.read on record:123</c>. Structural
/// relationships (belonging, serving) are facts owned by other services and
/// written here through the relationship write boundary; permission-granting
/// tuples may carry the exact permissions they grant on the object.
/// </summary>
public sealed class AuthorizationRelationship : AggregateRoot<Guid>
{
    private readonly List<string> _permissions = [];

    public Guid SubjectId { get; private set; }
    public string Relation { get; private set; }
    public string ObjectType { get; private set; }
    public Guid ObjectId { get; private set; }

    public IReadOnlyList<string> Permissions => _permissions.AsReadOnly();

    private AuthorizationRelationship(
        Guid id, Guid subjectId, string relation, string objectType, Guid objectId) : base(id)
    {
        SubjectId = subjectId;
        Relation = relation;
        ObjectType = objectType;
        ObjectId = objectId;
    }

    public static AuthorizationRelationship Create(
        Guid subjectId,
        string relation,
        string objectType,
        Guid objectId,
        IReadOnlyList<string>? permissions = null)
    {
        Guard.NotDefault(subjectId, nameof(subjectId));
        Guard.NotNullOrWhiteSpace(relation, nameof(relation));
        Guard.NotNullOrWhiteSpace(objectType, nameof(objectType));
        Guard.MaxLength(relation, 100, nameof(relation));
        Guard.MaxLength(objectType, 128, nameof(objectType));
        Guard.NotDefault(objectId, nameof(objectId));
        if (!RelationName.IsValid(relation))
            throw new InvalidRelationException(relation);

        var relationship = new AuthorizationRelationship(
            Guid.NewGuid(), subjectId, relation.Trim(), objectType.Trim(), objectId);

        if (permissions is not null && permissions.Count != 0)
        {
            var invalid = permissions.FirstOrDefault(p => !PermissionName.IsValid(p));
            if (invalid is not null)
                throw new InvalidPermissionException(invalid);

            relationship._permissions.AddRange(
                permissions.Select(PermissionName.Normalize).Distinct(StringComparer.Ordinal));
        }

        relationship.RaiseDomainEvent(new RelationshipWrittenEvent(
            relationship.Id, subjectId, relationship.Relation, relationship.ObjectType, relationship.ObjectId));

        return relationship;
    }

    public bool GrantsPermission(string permission) =>
        _permissions.Contains(permission, StringComparer.Ordinal);

    public bool References(Guid subjectId, string relation, string objectType, Guid objectId) =>
        SubjectId == subjectId &&
        string.Equals(Relation, relation, StringComparison.Ordinal) &&
        string.Equals(ObjectType, objectType, StringComparison.Ordinal) &&
        ObjectId == objectId;
}
