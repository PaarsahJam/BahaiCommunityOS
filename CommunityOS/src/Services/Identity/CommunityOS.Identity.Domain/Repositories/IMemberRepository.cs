using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.ValueObjects;

namespace CommunityOS.Identity.Domain.Repositories;

public interface IMemberRepository
{
    Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Member?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<IReadOnlyList<Member>> GetByLocalUnitAsync(Guid localUnitId, CancellationToken ct = default);
    Task AddAsync(Member member, CancellationToken ct = default);
    Task UpdateAsync(Member member, CancellationToken ct = default);
}
