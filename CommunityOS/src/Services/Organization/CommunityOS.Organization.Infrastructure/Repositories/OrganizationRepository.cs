using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Organization.Infrastructure.Repositories;

using Organization = CommunityOS.Organization.Domain.Aggregates.Organization;

public sealed class OrganizationRepository(OrganizationDbContext db) : IOrganizationRepository
{
    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Organizations.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Organization?> GetByNameAsync(string name, CancellationToken ct = default) =>
        await db.Organizations.FirstOrDefaultAsync(x => x.Name == name, ct);

    public async Task<IReadOnlyList<Organization>> ListAsync(CancellationToken ct = default) =>
        await db.Organizations.AsNoTracking().OrderBy(x => x.CreatedOn).ToListAsync(ct);

    public async Task AddAsync(Organization organization, CancellationToken ct = default)
    {
        await db.Organizations.AddAsync(organization, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Organization organization, CancellationToken ct = default)
    {
        db.Organizations.Update(organization);
        await db.SaveChangesAsync(ct);
    }
}