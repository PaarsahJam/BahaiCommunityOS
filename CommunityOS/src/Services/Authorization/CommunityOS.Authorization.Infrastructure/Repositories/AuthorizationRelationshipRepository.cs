using CommunityOS.Authorization.Domain.Aggregates;
using CommunityOS.Authorization.Domain.Repositories;
using CommunityOS.Authorization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Authorization.Infrastructure.Repositories;

public sealed class AuthorizationRelationshipRepository(AuthorizationDbContext db)
    : IAuthorizationRelationshipRepository
{
    public async Task<AuthorizationRelationship?> GetByIdAsync(
        Guid id, CancellationToken ct = default) =>
        await db.Relationships.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<AuthorizationRelationship>> ListBySubjectAsync(
        Guid subjectId, CancellationToken ct = default) =>
        await db.Relationships
            .Where(x => x.SubjectId == subjectId)
            .OrderBy(x => x.ObjectType)
            .ThenBy(x => x.ObjectId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AuthorizationRelationship>> ListAsync(
        Guid? subjectId = null,
        string? relation = null,
        string? objectType = null,
        Guid? objectId = null,
        CancellationToken ct = default)
    {
        var query = db.Relationships.AsNoTracking();

        if (subjectId is not null)
            query = query.Where(x => x.SubjectId == subjectId);
        if (!string.IsNullOrWhiteSpace(relation))
            query = query.Where(x => x.Relation == relation);
        if (!string.IsNullOrWhiteSpace(objectType))
            query = query.Where(x => x.ObjectType == objectType);
        if (objectId is not null)
            query = query.Where(x => x.ObjectId == objectId);

        return await query.OrderBy(x => x.SubjectId).ToListAsync(ct);
    }

    public async Task AddAsync(AuthorizationRelationship relationship, CancellationToken ct = default)
    {
        await db.Relationships.AddAsync(relationship, ct);
        await db.SaveChangesAsync(ct);
    }
}
