using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Authorization.Infrastructure.Repositories;

public sealed class RoleRepository(AuthorizationDbContext db) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Roles.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Role?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        await db.Roles.FirstOrDefaultAsync(x => x.Code == code, ct);

    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken ct = default) =>
        await db.Roles.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct);

    public async Task AddAsync(Role role, CancellationToken ct = default)
    {
        await db.Roles.AddAsync(role, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Role role, CancellationToken ct = default)
    {
        db.Roles.Update(role);
        await db.SaveChangesAsync(ct);
    }
}
