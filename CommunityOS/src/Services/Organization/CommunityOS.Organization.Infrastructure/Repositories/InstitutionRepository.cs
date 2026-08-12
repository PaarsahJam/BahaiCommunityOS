using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Institution = CommunityOS.Organization.Domain.Aggregates.Institution;

namespace CommunityOS.Organization.Infrastructure.Repositories;

public sealed class InstitutionRepository(OrganizationDbContext db) : IInstitutionRepository
{
    public async Task<Institution?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Institutions.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Institution?> GetByNameAsync(string name, CancellationToken ct = default) =>
        await db.Institutions.FirstOrDefaultAsync(x => x.Name == name, ct);

    public async Task<IReadOnlyList<Institution>> ListAsync(CancellationToken ct = default) =>
        await db.Institutions.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);

    public async Task AddAsync(Institution institution, CancellationToken ct = default)
    {
        await db.Institutions.AddAsync(institution, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Institution institution, CancellationToken ct = default)
    {
        db.Institutions.Update(institution);
        await db.SaveChangesAsync(ct);
    }
}