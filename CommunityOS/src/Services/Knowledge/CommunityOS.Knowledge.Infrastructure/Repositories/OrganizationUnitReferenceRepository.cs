using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Knowledge.Infrastructure.Repositories;

internal sealed class OrganizationUnitReferenceRepository(KnowledgeDbContext db)
    : IOrganizationUnitReferenceRepository
{
    public async Task<OrganizationUnitReference?> GetUnitByIdAsync(
        Guid organizationUnitId, CancellationToken ct = default) =>
        await db.OrganizationUnitReferences.FirstOrDefaultAsync(r => r.Id == organizationUnitId, ct);

    public async Task UpsertUnitAsync(
        OrganizationUnitReference reference, CancellationToken ct = default)
    {
        var existing = await db.OrganizationUnitReferences
            .FirstOrDefaultAsync(r => r.Id == reference.Id, ct);
        if (existing is null)
            db.OrganizationUnitReferences.Add(reference);
        else
            existing.Sync(reference.Name, reference.UnitType, reference.ParentId, reference.LastSeenOn);

        await db.SaveChangesAsync(ct);
    }
}