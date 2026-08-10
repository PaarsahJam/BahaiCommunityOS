using CommunityOS.Authorization.Domain.Aggregates;

namespace CommunityOS.Authorization.Domain.Repositories;

public interface IRoleAssignmentRepository
{
    Task<RoleAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RoleAssignment>> ListBySubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<IReadOnlyList<RoleAssignment>> ListByRoleAsync(Guid roleId, CancellationToken ct = default);
    Task AddAsync(RoleAssignment assignment, CancellationToken ct = default);
    Task UpdateAsync(RoleAssignment assignment, CancellationToken ct = default);
}
