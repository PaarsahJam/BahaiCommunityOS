using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Knowledge.Infrastructure.Repositories;

public sealed class ReferenceRepository(KnowledgeDbContext db) : IReferenceRepository
{
    public async Task<Reference?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.References.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Reference>> ListByOwnerAsync(
        ReferenceOwnerType ownerType, Guid ownerId, CancellationToken ct = default) =>
        await db.References.AsNoTracking()
            .Where(r => r.OwnerType == ownerType && r.OwnerId == ownerId)
            .ToListAsync(ct);

    public async Task AddAsync(Reference reference, CancellationToken ct = default)
    {
        await db.References.AddAsync(reference, ct);
        await db.SaveChangesAsync(ct);
    }
}