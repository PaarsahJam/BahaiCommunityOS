using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Reporting.Domain.Events;

public sealed record ReportRequestedEvent(Guid ReportId, string ReportType) : DomainEvent;

public sealed record ReportGeneratedEvent(Guid ReportId) : DomainEvent;
