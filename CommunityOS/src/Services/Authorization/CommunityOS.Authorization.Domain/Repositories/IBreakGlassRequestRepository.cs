using CommunityOS.Authorization.Domain.Aggregates;

namespace CommunityOS.Authorization.Domain.Repositories;

public interface IBreakGlassRequestRepository
{
    Task<BreakGlassRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BreakGlassRequest>> ListByRequesterAsync(Guid requesterId, CancellationToken ct = default);
    Task<IReadOnlyList<BreakGlassRequest>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(BreakGlassRequest request, CancellationToken ct = default);
    Task UpdateAsync(BreakGlassRequest request, CancellationToken ct = default);
}
