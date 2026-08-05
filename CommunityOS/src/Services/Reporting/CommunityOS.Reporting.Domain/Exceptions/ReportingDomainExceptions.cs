namespace CommunityOS.Reporting.Domain.Exceptions;

public sealed class ReportNotFoundException(Guid id)
    : Exception($"Report '{id}' was not found.");
