using CommunityOS.Community.Domain.Aggregates;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Community.Infrastructure.Repositories;

public sealed class PersonRepository(CommunityDbContext db) : IPersonRepository
{
    public async Task<Person?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Persons
            .Include(p => p.ContactMethods)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Person?> GetByIdentityAccountIdAsync(
        Guid identityAccountId, CancellationToken ct = default) =>
        await db.Persons
            .Include(p => p.ContactMethods)
            .FirstOrDefaultAsync(p => p.IdentityAccountId == identityAccountId, ct);

    public async Task<IReadOnlyList<Person>> ListAsync(CancellationToken ct = default) =>
        await db.Persons
            .Include(p => p.ContactMethods)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await db.Persons.AnyAsync(p => p.Id == id, ct);

    public async Task AddAsync(Person person, CancellationToken ct = default)
    {
        await db.Persons.AddAsync(person, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Person person, CancellationToken ct = default)
    {
        db.Persons.Update(person);
        await db.SaveChangesAsync(ct);
    }
}
