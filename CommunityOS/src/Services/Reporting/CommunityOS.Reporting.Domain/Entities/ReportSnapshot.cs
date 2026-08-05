using CommunityOS.Reporting.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Reporting.Domain.Entities;

public sealed class ReportSnapshot : Entity<Guid>
{
    private readonly List<Metric> _metrics = [];

    public DateTime CapturedAt { get; }
    public IReadOnlyList<Metric> Metrics => _metrics.AsReadOnly();

    private ReportSnapshot(Guid id) : base(id)
    {
        CapturedAt = DateTime.UtcNow;
    }

    public static ReportSnapshot Capture() => new(Guid.NewGuid());

    public void AddMetric(Metric metric) => _metrics.Add(metric);
}
