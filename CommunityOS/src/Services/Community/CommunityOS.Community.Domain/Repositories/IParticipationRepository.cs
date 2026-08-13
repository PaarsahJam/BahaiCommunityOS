using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Enumerations;

namespace CommunityOS.Community.Domain.Repositories;

public interface IParticipationRepository
{
    Task<Participation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Participation>> ListByPersonAsync(Guid personId, CancellationToken ct = default);
    Task<IReadOnlyList<Participation>> ListByTargetAsync(
        ParticipationTargetType targetType,
        Guid targetId,
        CancellationToken ct = default);
    Task<bool> ExistsAsync(
        Guid personId,
        ParticipationTargetType targetType,
        Guid targetId,
        CancellationToken ct = default);
    Task AddAsync(Participation participation, CancellationToken ct = default);
    Task UpdateAsync(Participation participation, CancellationToken ct = default);
}
