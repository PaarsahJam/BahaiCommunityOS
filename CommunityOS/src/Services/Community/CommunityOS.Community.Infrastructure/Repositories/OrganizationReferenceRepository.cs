using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Repositories;

internal sealed class OrganizationReferenceRepository(CommunityDbContext db)
    : IOrganizationReferenceRepository
{
    public async Task<OrganizationReference?> GetByIdAsync(
        Guid organizationId, CancellationToken ct = default) =>
        await db.OrganizationReferences.FirstOrDefaultAsync(r => r.Id == organizationId, ct);

    public async Task UpsertAsync(
        OrganizationReference reference, CancellationToken ct = default)
    {
        var existing = await db.OrganizationReferences
            .FirstOrDefaultAsync(r => r.Id == reference.Id, ct);
        if (existing is null)
            db.OrganizationReferences.Add(reference);
        else
            existing.Sync(
                reference.Name,
                reference.OrganizationType,
                reference.JurisdictionType,
                reference.JurisdictionScopeId,
                reference.LastSeenOn);

        await db.SaveChangesAsync(ct);
    }

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
