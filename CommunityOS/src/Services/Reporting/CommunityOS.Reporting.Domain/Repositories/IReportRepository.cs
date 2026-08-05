using CommunityOS.Reporting.Domain.Aggregates;
using CommunityOS.Reporting.Domain.ValueObjects;

namespace CommunityOS.Reporting.Domain.Repositories;

public interface IReportRepository
{
    Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetByScopeAsync(Guid scopeId, CancellationToken ct = default);
    Task<IReadOnlyList<Report>> GetByTypeAndPeriodAsync(ReportType type, DateRange period, CancellationToken ct = default);
    Task AddAsync(Report report, CancellationToken ct = default);
    Task UpdateAsync(Report report, CancellationToken ct = default);
}
