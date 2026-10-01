using CommunityOS.Identity.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CommunityOS.Identity.Infrastructure.Persistence;

/// <summary>
/// ADR-036 D3: explicit EF Core/Npgsql transaction over the single
/// <see cref="IdentityDbContext"/> shared by the repositories, so the account
/// row lock, the epoch decision and the refresh/emergency persistence all
/// commit as one unit. Entity SaveChanges performed through the repositories
/// inside the transaction are flushes only; <see cref="CommitAsync"/> performs
/// the final flush and the single commit (compatible with the MassTransit
/// transactional outbox: any outbox rows captured before the commit land in
/// the same transaction).
/// </summary>
public sealed class IdentityUnitOfWork(IdentityDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A unit-of-work transaction is already active.");

        _transaction = await db.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No unit-of-work transaction is active.");

        await db.SaveChangesAsync(ct);
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;

        // Discard entities whose pending changes belonged to the aborted unit
        // so a later operation in the same scope cannot flush them by accident.
        db.ChangeTracker.Clear();
    }
}
