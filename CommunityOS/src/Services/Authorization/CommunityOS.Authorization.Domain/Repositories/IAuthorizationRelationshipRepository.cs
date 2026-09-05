using CommunityOS.Authorization.Domain.Aggregates;

namespace CommunityOS.Authorization.Domain.Repositories;

public interface IAuthorizationRelationshipRepository
{
    Task<AuthorizationRelationship?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AuthorizationRelationship>> ListBySubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<IReadOnlyList<AuthorizationRelationship>> ListAsync(
        Guid? subjectId = null,
        string? relation = null,
        string? objectType = null,
        Guid? objectId = null,
        CancellationToken ct = default);
    Task AddAsync(AuthorizationRelationship relationship, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
