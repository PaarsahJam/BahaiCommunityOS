using CommunityOS.Content.Domain.Aggregates;

namespace CommunityOS.Content.Domain.Repositories;

public interface IAnnouncementRepository
{
    Task<Announcement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Announcement>> GetByLocalUnitAsync(Guid localUnitId, CancellationToken ct = default);
    Task AddAsync(Announcement announcement, CancellationToken ct = default);
    Task UpdateAsync(Announcement announcement, CancellationToken ct = default);
}

public interface ILibraryItemRepository
{
    Task<LibraryItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LibraryItem>> GetByCategoryAsync(string category, CancellationToken ct = default);
    Task AddAsync(LibraryItem item, CancellationToken ct = default);
    Task UpdateAsync(LibraryItem item, CancellationToken ct = default);
}
