using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Identity.Infrastructure.Repositories;

internal sealed class MemberRepository(IdentityDbContext db) : IMemberRepository
{
    public async Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Members
            .Include(m => m.Roles)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<Member?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
        await db.Members
            .Include(m => m.Roles)
            .FirstOrDefaultAsync(m => m.Email.Value == email.Value, ct);

    public async Task<IReadOnlyList<Member>> GetByLocalUnitAsync(
        Guid localUnitId, CancellationToken ct = default) =>
        await db.Members
            .Include(m => m.Roles)
            .Where(m => m.LocalUnitId == localUnitId)
            .ToListAsync(ct);

    public async Task AddAsync(Member member, CancellationToken ct = default)
    {
        await db.Members.AddAsync(member, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Member member, CancellationToken ct = default)
    {
        db.Members.Update(member);
        await db.SaveChangesAsync(ct);
    }
}
