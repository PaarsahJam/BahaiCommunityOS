using CommunityOS.Documents.Domain.Aggregates;

namespace CommunityOS.Documents.Domain.Repositories;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Document>> ListByOrganizationUnitAsync(
        Guid organizationUnitId, CancellationToken ct = default);

    Task AddAsync(Document document, CancellationToken ct = default);

    Task UpdateAsync(Document document, CancellationToken ct = default);
}