using CommunityOS.Reporting.Domain.Entities;
using CommunityOS.Reporting.Domain.Events;
using CommunityOS.Reporting.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Reporting.Domain.Aggregates;

public sealed class Report : AggregateRoot<Guid>
{
    private readonly List<ReportSnapshot> _snapshots = [];

    public ReportType Type { get; private set; }
    public DateRange Period { get; private set; }
    public Guid RequestedById { get; private set; }
    public Guid ScopeId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<ReportSnapshot> Snapshots => _snapshots.AsReadOnly();

    private Report(Guid id, ReportType type, DateRange period,
        Guid requestedById, Guid scopeId) : base(id)
    {
        Type = type;
        Period = period;
        RequestedById = requestedById;
        ScopeId = scopeId;
        CreatedAt = DateTime.UtcNow;
    }

    public static Report Request(ReportType type, DateRange period,
        Guid requestedById, Guid scopeId)
    {
        Guard.NotNull(type, nameof(type));
        Guard.NotNull(period, nameof(period));
        Guard.NotDefault(requestedById, nameof(requestedById));
        Guard.NotDefault(scopeId, nameof(scopeId));
        var report = new Report(Guid.NewGuid(), type, period, requestedById, scopeId);
        report.RaiseDomainEvent(new ReportRequestedEvent(report.Id, type.Name));
        return report;
    }

    public void AddSnapshot(ReportSnapshot snapshot)
    {
        Guard.NotNull(snapshot, nameof(snapshot));
        _snapshots.Add(snapshot);
        RaiseDomainEvent(new ReportGeneratedEvent(Id));
    }
}
