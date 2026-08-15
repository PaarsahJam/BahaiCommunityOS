using CommunityOS.Documents.Domain.Aggregates;
using CommunityOS.Documents.Domain.Repositories;
using CommunityOS.Documents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Documents.Infrastructure.Repositories;

public sealed class OrganizationUnitReferenceRepository(DocumentsDbContext db)
    : IOrganizationUnitReferenceRepository
{
    public async Task<OrganizationUnitReference?> GetUnitByIdAsync(
        Guid organizationUnitId, CancellationToken ct = default) =>
        await db.OrganizationUnitReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == organizationUnitId, ct);

    public async Task UpsertUnitAsync(OrganizationUnitReference reference, CancellationToken ct = default)
    {
        var existing = await db.OrganizationUnitReferences
            .FirstOrDefaultAsync(r => r.Id == reference.Id, ct);

        if (existing is null)
            await db.OrganizationUnitReferences.AddAsync(reference, ct);
        else
            db.OrganizationUnitReferences.Entry(existing).CurrentValues.SetValues(reference);

        await db.SaveChangesAsync(ct);
    }
}