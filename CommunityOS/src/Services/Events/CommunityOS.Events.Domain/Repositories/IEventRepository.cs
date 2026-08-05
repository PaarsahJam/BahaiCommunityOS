using CommunityOS.Events.Domain.Aggregates;

namespace CommunityOS.Events.Domain.Repositories;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Event>> GetByLocalUnitAsync(Guid localUnitId, CancellationToken ct = default);
    Task<IReadOnlyList<Event>> GetUpcomingAsync(DateTime from, CancellationToken ct = default);
    Task AddAsync(Event @event, CancellationToken ct = default);
    Task UpdateAsync(Event @event, CancellationToken ct = default);
}
