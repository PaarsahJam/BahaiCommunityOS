using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Repositories;
using CommunityOS.Records.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Records.Infrastructure.Repositories;

public sealed class RetentionScheduleRepository(RecordsDbContext db) : IRetentionScheduleRepository
{
    public Task<RetentionSchedule?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.RetentionSchedules
            .Include(s => s.Rules)
            .FirstOrDefaultAsync(s => s.Code == code, cancellationToken);

    public Task<List<RetentionSchedule>> ListAsync(CancellationToken cancellationToken = default) =>
        db.RetentionSchedules
            .Include(s => s.Rules)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.RetentionSchedules.AnyAsync(s => s.Code == code, cancellationToken);

    public async Task AddAsync(RetentionSchedule schedule, CancellationToken cancellationToken = default)
    {
        db.RetentionSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RetentionSchedule schedule, CancellationToken cancellationToken = default)
    {
        db.RetentionSchedules.Update(schedule);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RecordCategoryRepository(RecordsDbContext db) : IRecordCategoryRepository
{
    public Task<RecordCategory?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.RecordCategories.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);

    public Task<List<RecordCategory>> ListAsync(CancellationToken cancellationToken = default) =>
        db.RecordCategories.OrderBy(c => c.Code).ToListAsync(cancellationToken);

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.RecordCategories.AnyAsync(c => c.Code == code, cancellationToken);

    public async Task AddAsync(RecordCategory category, CancellationToken cancellationToken = default)
    {
        db.RecordCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RecordCategory category, CancellationToken cancellationToken = default)
    {
        db.RecordCategories.Update(category);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RecordsOrganizationUnitReferenceRepository(RecordsDbContext db)
    : IOrganizationUnitReferenceRepository
{
    public Task<bool> ExistsAsync(Guid organizationUnitId, CancellationToken cancellationToken = default) =>
        db.OrganizationUnitReferences.AnyAsync(r => r.OrganizationUnitId == organizationUnitId, cancellationToken);

    public async Task AddAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default)
    {
        db.OrganizationUnitReferences.Add(reference);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OrganizationUnitReference reference, CancellationToken cancellationToken = default)
    {
        db.OrganizationUnitReferences.Update(reference);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var reference = await db.OrganizationUnitReferences
            .FirstOrDefaultAsync(r => r.OrganizationUnitId == organizationUnitId, cancellationToken);
        if (reference is not null)
        {
            db.OrganizationUnitReferences.Remove(reference);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}