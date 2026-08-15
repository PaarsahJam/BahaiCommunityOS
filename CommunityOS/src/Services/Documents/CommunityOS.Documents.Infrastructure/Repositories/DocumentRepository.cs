using CommunityOS.Documents.Domain.Aggregates;
using CommunityOS.Documents.Domain.Repositories;
using CommunityOS.Documents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Documents.Infrastructure.Repositories;

public sealed class DocumentRepository(DocumentsDbContext db) : IDocumentRepository
{
    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct = default) =>
        await db.Documents.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<Document>> ListByOrganizationUnitAsync(
        Guid organizationUnitId, CancellationToken ct = default) =>
        await db.Documents
            .AsNoTracking()
            .Where(d => d.OrganizationUnitId == organizationUnitId ||
                        d.Scopes.Any(s => s.OrganizationUnitId == organizationUnitId))
            .ToListAsync(ct);

    public async Task AddAsync(Document document, CancellationToken ct = default)
    {
        await db.Documents.AddAsync(document, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Document document, CancellationToken ct = default)
    {
        db.Documents.Update(document);
        await db.SaveChangesAsync(ct);
    }
}