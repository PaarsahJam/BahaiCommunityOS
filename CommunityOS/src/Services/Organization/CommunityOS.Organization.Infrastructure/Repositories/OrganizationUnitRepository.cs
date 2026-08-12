using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Repositories;
using CommunityOS.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Organization.Infrastructure.Repositories;

public sealed class OrganizationUnitRepository(OrganizationDbContext db) : IOrganizationUnitRepository
{
    public async Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.OrganizationUnits
            .Include(x => x.Parents)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<OrganizationUnit>> ListByOrganizationAsync(
        Guid organizationId, CancellationToken ct = default) =>
        await db.OrganizationUnits
            .AsNoTracking()
            .Include(x => x.Parents)
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrganizationUnit>> ListChildrenAsync(
        Guid parentId, DateTime? asOf = null, CancellationToken ct = default)
    {
        var moment = asOf?.ToUniversalTime() ?? DateTime.UtcNow;
        return await db.OrganizationUnits
            .AsNoTracking()
            .Include(x => x.Parents)
            .Where(x => x.Parents.Any(p =>
                p.ParentId == parentId &&
                p.Period.EffectiveFrom <= moment &&
                (p.Period.EffectiveUntil == null || p.Period.EffectiveUntil > moment)))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OrganizationUnit>> ListRootsAsync(
        Guid organizationId, DateTime? asOf = null, CancellationToken ct = default)
    {
        var moment = asOf?.ToUniversalTime() ?? DateTime.UtcNow;
        return await db.OrganizationUnits
            .AsNoTracking()
            .Include(x => x.Parents)
            .Where(x => x.OrganizationId == organizationId &&
                        !x.Parents.Any(p =>
                            p.ParentId != null &&
                            p.Period.EffectiveFrom <= moment &&
                            (p.Period.EffectiveUntil == null || p.Period.EffectiveUntil > moment)))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListAncestorIdsAsync(
        Guid organizationUnitId, DateTime? asOf = null, CancellationToken ct = default)
    {
        var moment = asOf?.ToUniversalTime() ?? DateTime.UtcNow;
        var ancestors = new List<Guid>();
        var seen = new HashSet<Guid>();

        Guid? currentId = organizationUnitId;
        while (currentId is { } id && seen.Add(id))
        {
            var unit = await db.OrganizationUnits
                .AsNoTracking()
                .Include(x => x.Parents)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (unit is null)
                break;

            var parent = unit.Parents
                .Where(p => p.IsEffectiveAt(moment))
                .OrderByDescending(p => p.Period.EffectiveFrom)
                .FirstOrDefault(p => p.ParentId is not null);

            if (parent?.ParentId is not { } parentId)
                break;

            ancestors.Add(parentId);
            currentId = parentId;
        }

        return ancestors;
    }

    public async Task<IReadOnlyList<OrganizationUnit>> ListDescendantsAsync(
        Guid organizationUnitId, DateTime? asOf = null, CancellationToken ct = default)
    {
        var moment = asOf?.ToUniversalTime() ?? DateTime.UtcNow;
        var result = new List<OrganizationUnit>();
        var seen = new HashSet<Guid> { organizationUnitId };
        var queue = new Queue<Guid>();
        queue.Enqueue(organizationUnitId);

        while (queue.Count != 0)
        {
            var currentId = queue.Dequeue();
            var children = await ListChildrenAsync(currentId, moment, ct);
            foreach (var child in children)
            {
                if (seen.Add(child.Id))
                {
                    result.Add(child);
                    queue.Enqueue(child.Id);
                }
            }
        }

        return result;
    }

    public async Task<bool> IsDescendantAsync(
        Guid candidateDescendantId, Guid ancestorId, DateTime? asOf = null, CancellationToken ct = default)
    {
        var moment = asOf?.ToUniversalTime() ?? DateTime.UtcNow;
        if (candidateDescendantId == ancestorId)
            return true;

        var ancestors = await ListAncestorIdsAsync(candidateDescendantId, moment, ct);
        return ancestors.Contains(ancestorId);
    }

    public async Task AddAsync(OrganizationUnit unit, CancellationToken ct = default)
    {
        await db.OrganizationUnits.AddAsync(unit, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(OrganizationUnit unit, CancellationToken ct = default)
    {
        db.OrganizationUnits.Update(unit);
        await db.SaveChangesAsync(ct);
    }
}