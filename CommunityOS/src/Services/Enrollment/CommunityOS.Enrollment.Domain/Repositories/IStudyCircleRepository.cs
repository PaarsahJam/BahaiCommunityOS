using CommunityOS.Enrollment.Domain.Aggregates;

namespace CommunityOS.Enrollment.Domain.Repositories;

public interface IStudyCircleRepository
{
    Task<StudyCircle?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StudyCircle>> GetByLocalUnitAsync(Guid localUnitId, CancellationToken ct = default);
    Task<IReadOnlyList<StudyCircle>> GetByFacilitatorAsync(Guid facilitatorId, CancellationToken ct = default);
    Task AddAsync(StudyCircle circle, CancellationToken ct = default);
    Task UpdateAsync(StudyCircle circle, CancellationToken ct = default);
}
