using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;

namespace CommunityOS.Community.Domain.Repositories;

public interface IFamilyRelationshipRepository
{
    Task<FamilyRelationship?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FamilyRelationship>> ListByPersonAsync(Guid personId, CancellationToken ct = default);
    Task<bool> ExistsActiveAsync(
        Guid personIdA,
        Guid personIdB,
        RelationshipType relationshipType,
        CancellationToken ct = default);
    Task AddAsync(FamilyRelationship relationship, CancellationToken ct = default);
    Task UpdateAsync(FamilyRelationship relationship, CancellationToken ct = default);
}
