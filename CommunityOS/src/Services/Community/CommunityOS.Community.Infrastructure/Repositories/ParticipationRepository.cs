using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Repositories;

public sealed class ParticipationRepository(CommunityDbContext db)
    : IParticipationRepository
{
    public async Task<Participation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Participations.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Participation>> ListByPersonAsync(
        Guid personId, CancellationToken ct = default) =>
        await db.Participations
            .Where(p => p.PersonId == personId)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Participation>> ListByTargetAsync(
        ParticipationTargetType targetType,
        Guid targetId,
        CancellationToken ct = default) =>
        await db.Participations
            .Where(p => p.TargetType == targetType && p.TargetId == targetId)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(
        Guid personId,
        ParticipationTargetType targetType,
        Guid targetId,
        CancellationToken ct = default) =>
        await db.Participations.AnyAsync(
            p => p.PersonId == personId && p.TargetType == targetType && p.TargetId == targetId,
            ct);

    public async Task AddAsync(Participation participation, CancellationToken ct = default)
    {
        await db.Participations.AddAsync(participation, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Participation participation, CancellationToken ct = default)
    {
        db.Participations.Update(participation);
        await db.SaveChangesAsync(ct);
    }
}
