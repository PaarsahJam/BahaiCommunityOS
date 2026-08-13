using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Repositories;

public sealed class FamilyRelationshipRepository(CommunityDbContext db)
    : IFamilyRelationshipRepository
{
    public async Task<FamilyRelationship?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.FamilyRelationships.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<FamilyRelationship>> ListByPersonAsync(
        Guid personId, CancellationToken ct = default) =>
        await db.FamilyRelationships
            .Where(r => r.PersonIdA == personId || r.PersonIdB == personId)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<bool> ExistsActiveAsync(
        Guid personIdA,
        Guid personIdB,
        RelationshipType relationshipType,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await db.FamilyRelationships.AnyAsync(
            r => r.PersonIdA == personIdA &&
                 r.PersonIdB == personIdB &&
                 r.RelationshipType == relationshipType &&
                 r.Period.EffectiveFrom <= now &&
                 (r.Period.EffectiveUntil == null || r.Period.EffectiveUntil > now),
            ct);
    }

    public async Task AddAsync(FamilyRelationship relationship, CancellationToken ct = default)
    {
        await db.FamilyRelationships.AddAsync(relationship, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(FamilyRelationship relationship, CancellationToken ct = default)
    {
        db.FamilyRelationships.Update(relationship);
        await db.SaveChangesAsync(ct);
    }
}
