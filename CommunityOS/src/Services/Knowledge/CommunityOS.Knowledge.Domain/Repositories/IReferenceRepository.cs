using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Enumerations;

namespace CommunityOS.Knowledge.Domain.Repositories;

public interface IReferenceRepository
{
    Task<Reference?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Reference>> ListByOwnerAsync(ReferenceOwnerType ownerType, Guid ownerId, CancellationToken ct = default);
    Task AddAsync(Reference reference, CancellationToken ct = default);
}