using CommunityOS.Records.Domain.Aggregates;

namespace CommunityOS.Records.Domain.Repositories;

/// <summary>
/// Persistence abstraction for the Records aggregate (ADR-023). Implementations
/// live in the Infrastructure layer (EF Core). Methods return nullable results
/// and throw nothing — command handlers translate a null into the corresponding
/// domain exception so read-404 and write-404 stay indistinguishable.
/// </summary>
public interface IRecordRepository
{
    Task<Record?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<Record>> ListAsync(CancellationToken cancellationToken = default);

    Task<Record?> GetByHoldIdAsync(Guid holdId, CancellationToken cancellationToken = default);

    Task<List<Record>> ListByOrganizationUnitAsync(
        Guid organizationUnitId,
        CancellationToken cancellationToken = default);

    /// <summary>Records that own at least one hold, used to list holds.</summary>
    Task<List<Record>> ListByHoldAsync(
        Guid? recordId,
        string? holdType,
        bool? activeOnly,
        CancellationToken cancellationToken = default);

    /// <summary>Records with an evidence reference to the given document.</summary>
    Task<List<Record>> ListByEvidenceDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<List<Record>> ListRetentionExpiredCandidatesAsync(
        DateTime asOf,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByCategoryCodeAsync(string categoryCode, CancellationToken cancellationToken = default);

    Task<bool> ExistsByRetentionScheduleCodeAsync(string code, CancellationToken cancellationToken = default);

    Task AddAsync(Record record, CancellationToken cancellationToken = default);

    Task UpdateAsync(Record record, CancellationToken cancellationToken = default);
}

/// <summary>Persistence abstraction for the RetentionSchedule aggregate.</summary>
public interface IRetentionScheduleRepository
{
    Task<RetentionSchedule?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<List<RetentionSchedule>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task AddAsync(RetentionSchedule schedule, CancellationToken cancellationToken = default);

    Task UpdateAsync(RetentionSchedule schedule, CancellationToken cancellationToken = default);
}

/// <summary>Persistence abstraction for the RecordCategory catalog.</summary>
public interface IRecordCategoryRepository
{
    Task<RecordCategory?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<List<RecordCategory>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task AddAsync(RecordCategory category, CancellationToken cancellationToken = default);

    Task UpdateAsync(RecordCategory category, CancellationToken cancellationToken = default);
}

/// <summary>Persistence abstraction for the Organization unit read-model projection.</summary>
public interface IOrganizationUnitReferenceRepository
{
    Task<bool> ExistsAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    Task AddAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default);

    Task UpdateAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);
}