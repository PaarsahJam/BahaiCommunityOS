using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Authorization.Infrastructure.Repositories;

public sealed class RoleAssignmentRepository(AuthorizationDbContext db) : IRoleAssignmentRepository
{
    public async Task<RoleAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.RoleAssignments.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<RoleAssignment>> ListBySubjectAsync(
        Guid subjectId, CancellationToken ct = default) =>
        await db.RoleAssignments
            .Where(x => x.SubjectId == subjectId)
            .OrderBy(x => x.GrantedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RoleAssignment>> ListByRoleAsync(
        Guid roleId, CancellationToken ct = default) =>
        await db.RoleAssignments
            .Where(x => x.RoleId == roleId)
            .OrderBy(x => x.GrantedAt)
            .ToListAsync(ct);

    public async Task AddAsync(RoleAssignment assignment, CancellationToken ct = default)
    {
        await db.RoleAssignments.AddAsync(assignment, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(RoleAssignment assignment, CancellationToken ct = default)
    {
        db.RoleAssignments.Update(assignment);
        await db.SaveChangesAsync(ct);
    }
}
