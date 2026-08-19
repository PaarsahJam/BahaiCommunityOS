using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Enumerations;
using CommunityOS.Records.Domain.Repositories;
using CommunityOS.Records.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Records.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRecordRepository"/>. Owned collections
/// are included explicitly so the aggregate graph is fully materialized for
/// guard checks and mutations.
/// </summary>
public sealed class RecordRepository(RecordsDbContext db) : IRecordRepository
{
    public Task<Record?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Record?> GetByHoldIdAsync(Guid holdId, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(r => r.Holds.Any(h => h.Id == holdId), cancellationToken);

    public Task<List<Record>> ListAsync(CancellationToken cancellationToken = default) =>
        Query().OrderByDescending(r => r.UpdatedOn).ToListAsync(cancellationToken);

    public Task<List<Record>> ListByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default) =>
        Query()
            .Where(r => r.OrganizationUnitId == organizationUnitId || r.Scopes.Any(s => s.OrganizationUnitId == organizationUnitId))
            .OrderByDescending(r => r.UpdatedOn)
            .ToListAsync(cancellationToken);

    public Task<List<Record>> ListByHoldAsync(
        Guid? recordId, string? holdType, bool? activeOnly, CancellationToken cancellationToken = default) =>
        Query()
            .Where(r => recordId == null || r.Id == recordId)
            .Where(r => holdType == null || r.Holds.Any(h => h.HoldType == holdType))
            .Where(r => activeOnly == null || r.Holds.Any(h => h.IsActive == activeOnly))
            .ToListAsync(cancellationToken);

    public Task<List<Record>> ListByEvidenceDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        Query()
            .Where(r => r.Evidence.Any(e => e.DocumentId == documentId))
            .ToListAsync(cancellationToken);

    public Task<List<Record>> ListRetentionExpiredCandidatesAsync(DateTime asOf, CancellationToken cancellationToken = default) =>
        Query()
            .Where(r => r.Classification.RetentionScheduleCode != null)
            .Where(r => r.Classification.RetentionExpiredOn == null)
            .Where(r => r.Status != RecordStatus.Deactivated)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByCategoryCodeAsync(string categoryCode, CancellationToken cancellationToken = default) =>
        db.Records.AnyAsync(r => r.Category == categoryCode, cancellationToken);

    public Task<bool> ExistsByRetentionScheduleCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.Records.AnyAsync(r => r.Classification.RetentionScheduleCode == code, cancellationToken);

    public async Task AddAsync(Record record, CancellationToken cancellationToken = default)
    {
        db.Records.Add(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Record record, CancellationToken cancellationToken = default)
    {
        db.Records.Update(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Record> Query() =>
        db.Records
            .Include(r => r.WorkingFields)
            .Include(r => r.Versions).ThenInclude(v => v.Fields)
            .Include(r => r.Scopes)
            .Include(r => r.Evidence)
            .Include(r => r.Holds).ThenInclude(h => h.DocumentReferences);
}