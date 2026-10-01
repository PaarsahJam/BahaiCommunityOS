using CommunityOS.Identity.Application;
using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.Crypto;
using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.Identity.Infrastructure.Persistence;
using CommunityOS.Identity.Infrastructure.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace CommunityOS.Identity.IntegrationTests.Concurrency;

/// <summary>
/// ADR-036 D3/Q3: real-PostgreSQL concurrency verification of the EXISTING
/// refresh-token rotation / emergency-invalidation implementation against the
/// live PostgreSQL 18.6 instance at 127.0.0.1:5432. No mocks of the database,
/// locking or transactions: handlers are dispatched through the real MediatR
/// pipeline against the real IdentityDbContext/repositories, and every scope
/// is its own DbContext + connection.
/// </summary>
public sealed class RefreshRotationConcurrencyTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=communityos_identity;Username=communityos;Password=communityos";

    private readonly ServiceProvider _provider;
    private readonly List<Guid> _createdAccountIds = [];

    public RefreshRotationConcurrencyTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = ConnectionString,
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddIdentityApplication();
        services.AddDbContext<IdentityDbContext>(opts =>
            opts.UseNpgsql(ConnectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName)));
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<ISecurityEventRepository, SecurityEventRepository>();
        services.AddScoped<IUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<ITokenService, TestTokenService>();

        _provider = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupAsync();
        await _provider.DisposeAsync();
    }

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .Options;
        return new IdentityDbContext(options);
    }

    private async Task CleanupAsync()
    {
        if (_createdAccountIds.Count == 0)
            return;

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        foreach (var id in _createdAccountIds)
        {
            await using var cmd = new NpgsqlCommand(
                """
                DELETE FROM identity.security_events WHERE user_account_id = @id;
                DELETE FROM identity.sessions WHERE user_account_id = @id;
                DELETE FROM identity.user_accounts WHERE "Id" = @id;
                """, conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        _createdAccountIds.Clear();
    }

    // --- Seeding -------------------------------------------------------------

    private static (Session Session, string Token) MakeSession(
        Guid userAccountId, long epoch)
    {
        var token = $"RT-{Guid.NewGuid():N}";
        var session = Session.Create(
            userAccountId, Guid.NewGuid(), TokenHasher.Hash(token), TimeSpan.FromDays(30),
            sessionRevocationEpochAtIssue: epoch);
        return (session, token);
    }

    private async Task<UserAccount> SeedAsync(
        long epoch = 0, Action<UserAccount>? mutate = null)
    {
        await using var scope = _provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();

        var account = UserAccount.Register(
            Email.Create($"q3c-{Guid.NewGuid():N}@example.org"), "password-hash");
        mutate?.Invoke(account);
        await accounts.AddAsync(account);

        for (var i = 0; i < epoch; i++)
            account.AdvanceSessionRevocationEpochOnce();
        if (epoch > 0)
            await accounts.UpdateAsync(account);

        _createdAccountIds.Add(account.Id);
        return account;
    }

    private async Task<Session> AddSessionAsync(Session session)
    {
        await using var scope = _provider.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        await sessions.AddAsync(session);
        return session;
    }

    // --- Real handler dispatch (MediatR, real scoped context/transaction) ----

    private static async Task<TokenDto> RefreshAsync(IServiceProvider provider, string token)
    {
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new RefreshSessionCommand(token), CancellationToken.None);
    }

    private static async Task InvalidateAsync(IServiceProvider provider, Guid accountId)
    {
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new InvalidateAllSessionsCommand(accountId), CancellationToken.None);
    }

    private static async Task<(TokenDto? Ok, Exception? Error)> AttemptRefreshAsync(
        IServiceProvider provider, string token, Task startGate)
    {
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await startGate;
        var send = sender.Send(new RefreshSessionCommand(token), CancellationToken.None);
        try
        {
            return (await send, null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    // --- Readers ---------------------------------------------------------------

    private async Task<UserAccount?> LoadAccountAsync(Guid accountId)
    {
        await using var scope = _provider.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        return await accounts.GetByIdAsync(accountId);
    }

    private async Task<IReadOnlyList<Session>> LoadFamilyAsync(Guid familyId)
    {
        await using var scope = _provider.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        return await sessions.GetByFamilyAsync(familyId);
    }

    private async Task<IReadOnlyList<SecurityEvent>> LoadEventsAsync(Guid accountId)
    {
        await using var scope = _provider.CreateAsyncScope();
        var events = scope.ServiceProvider.GetRequiredService<ISecurityEventRepository>();
        return await events.GetByUserAsync(accountId, take: 100);
    }

    // --- Tests -----------------------------------------------------------------

    [Fact]
    public async Task Account_row_for_update_serializes_concurrent_transactions()
    {
        var account = await SeedAsync();

        var holderAtLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondAtLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var second = Task.Run(async () =>
        {
            await using var scope = _provider.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();

            await uow.BeginTransactionAsync();
            secondAtLock.SetResult();
            var observed = await accounts.GetByIdForUpdateAsync(account.Id);
            try
            {
                await uow.CommitAsync();
            }
            catch
            {
                await uow.RollbackAsync();
            }

            return observed;
        });

        // Transaction 1 acquires the account-row FOR UPDATE lock and holds it.
        await using (var holderScope = _provider.CreateAsyncScope())
        {
            var uow = holderScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var accounts = holderScope.ServiceProvider.GetRequiredService<IUserAccountRepository>();

            await uow.BeginTransactionAsync();
            var held = await accounts.GetByIdForUpdateAsync(account.Id);
            held.Should().NotBeNull();
            holderAtLock.SetResult();

            // Transaction 2 is now queued on the account row.
            await secondAtLock.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // It must NOT pass the lock while transaction 1 still holds the row.
            var blocking = async () => await second.WaitAsync(TimeSpan.FromMilliseconds(1500));
            await blocking.Should().ThrowAsync<TimeoutException>(
                "the second transaction must block on FOR UPDATE while the first holds the account row");

            await uow.CommitAsync();
        }

        // Once transaction 1 commits, transaction 2 proceeds past the lock.
        var result = await second.WaitAsync(TimeSpan.FromSeconds(10));
        result.Should().NotBeNull();
        result!.Id.Should().Be(account.Id);
    }

    [Fact]
    public async Task Refresh_rotation_commits_consumption_and_replacement_in_one_atomic_commit()
    {
        var account = await SeedAsync();
        var (first, token) = MakeSession(account.Id, epoch: 0);
        await AddSessionAsync(first);

        var rotated = await RefreshAsync(_provider, token);

        // Final committed state: old token consumed AND replacement present,
        // atomically — no half-committed state is ever visible.
        var family = await LoadFamilyAsync(first.TokenFamilyId);
        family.Should().HaveCount(2);
        var oldSession = family.Single(s => s.Id == first.Id);
        var replacement = family.Single(s => s.Id != first.Id);

        oldSession.RefreshTokenUsed.Should().BeTrue("rotation consumes the old token");
        oldSession.IsRevoked.Should().BeFalse("a healthy rotation does not revoke the family");
        replacement.RefreshTokenHash.Should().Be(TokenHasher.Hash(rotated.RefreshToken));
        replacement.SessionRevocationEpochAtIssue.Should().Be(0,
            "the replacement binds to the account epoch observed under the lock");

        // The consumed token is rejected on a later refresh.
        var act = async () => await RefreshAsync(_provider, token);
        await act.Should().ThrowExactlyAsync<RefreshTokenReuseDetectedException>();
    }

    [Fact]
    public async Task No_committed_intermediate_state_exists_for_consumption_without_replacement()
    {
        var account = await SeedAsync();
        var (first, token) = MakeSession(account.Id, epoch: 0);
        await AddSessionAsync(first);
        var nextToken = $"next-{Guid.NewGuid():N}";

        // Reproduce the exact repository write sequence a refresh performs inside
        // one explicit transaction, but stop BEFORE the final commit.
        await using (var writerScope = _provider.CreateAsyncScope())
        {
            var uow = writerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var sessions = writerScope.ServiceProvider.GetRequiredService<ISessionRepository>();

            await uow.BeginTransactionAsync();
            var loaded = await sessions.GetByIdAsync(first.Id);
            var replacement = loaded!.Rotate(
                TokenHasher.Hash(nextToken), TimeSpan.FromDays(30), account.SessionRevocationEpoch);
            await sessions.UpdateAsync(loaded, CancellationToken.None);
            await sessions.AddAsync(replacement, CancellationToken.None);

            // A concurrent reader (own connection, committed state only) sees
            // NEITHER the consumption NOR the replacement beforehand.
            await using (var readerScope = _provider.CreateAsyncScope())
            {
                var reader = readerScope.ServiceProvider.GetRequiredService<ISessionRepository>();
                var oldRead = await reader.GetByRefreshTokenHashAsync(TokenHasher.Hash(token));
                var newRead = await reader.GetByRefreshTokenHashAsync(TokenHasher.Hash(nextToken));

                oldRead.Should().NotBeNull();
                oldRead!.RefreshTokenUsed.Should().BeFalse(
                    "the in-transaction consumption flush is invisible to other transactions");
                newRead.Should().BeNull(
                    "the in-transaction replacement flush is invisible to other transactions — no half-committed state");
            }

            await uow.CommitAsync();
        }

        // After the single commit, both effects are visible together.
        var committed = await LoadFamilyAsync(first.TokenFamilyId);
        committed.Should().HaveCount(2);
        committed.Single(s => s.Id == first.Id).RefreshTokenUsed.Should().BeTrue();
        committed.Should().ContainSingle(s => s.RefreshTokenHash == TokenHasher.Hash(nextToken));
    }

    [Fact]
    public async Task RaceA_refresh_then_emergency_invalidation_revokes_the_rotated_family()
    {
        var account = await SeedAsync();
        var (first, token) = MakeSession(account.Id, epoch: 0);
        await AddSessionAsync(first);

        // Race A: the refresh transaction wins, then the emergency invalidates.
        var rotated = await RefreshAsync(_provider, token);
        await InvalidateAsync(_provider, account.Id);

        (await LoadAccountAsync(account.Id))!.SessionRevocationEpoch.Should().Be(1,
            "the emergency advances the epoch exactly once");

        var family = await LoadFamilyAsync(first.TokenFamilyId);
        family.Should().HaveCount(2, "the rotated replacement session was persisted");
        family.Should().OnlyContain(s => s.IsRevoked,
            "the emergency revokes BOTH the original and the newly rotated session");

        var act = async () => await RefreshAsync(_provider, rotated.RefreshToken);
        await act.Should().ThrowExactlyAsync<InvalidRefreshTokenException>(
            "the token issued by the race-winning rotation is dead");
    }

    [Fact]
    public async Task RaceB_emergency_invalidation_then_refresh_rejects_the_stale_family()
    {
        var account = await SeedAsync();
        var (first, token) = MakeSession(account.Id, epoch: 0);
        await AddSessionAsync(first);

        // Race B: the emergency commits first, then an old refresh arrives.
        await InvalidateAsync(_provider, account.Id);

        var act = async () => await RefreshAsync(_provider, token);
        await act.Should().ThrowExactlyAsync<InvalidRefreshTokenException>(
            "the refresh must observe the post-lock epoch and fail closed");

        (await LoadAccountAsync(account.Id))!.SessionRevocationEpoch.Should().Be(1);

        var family = await LoadFamilyAsync(first.TokenFamilyId);
        family.Should().ContainSingle();
        family[0].IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RaceB_rejects_on_the_post_lock_epoch_alone_even_when_the_family_is_still_active()
    {
        // Regression: a handler that used a PRE-lock cached epoch (0) would
        // ACCEPT this refresh. The post-lock epoch (1) must reject it.
        var account = await SeedAsync();
        var (first, token) = MakeSession(account.Id, epoch: 0);
        await AddSessionAsync(first);

        await using (var writerScope = _provider.CreateAsyncScope())
        {
            var uow = writerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var accounts = writerScope.ServiceProvider.GetRequiredService<IUserAccountRepository>();

            await uow.BeginTransactionAsync();
            var loaded = await accounts.GetByIdAsync(account.Id);
            loaded!.AdvanceSessionRevocationEpochOnce();
            await accounts.UpdateAsync(loaded, CancellationToken.None);
            await uow.CommitAsync();
        }

        (await LoadAccountAsync(account.Id))!.SessionRevocationEpoch.Should().Be(1);

        var act = async () => await RefreshAsync(_provider, token);
        await act.Should().ThrowExactlyAsync<InvalidRefreshTokenException>(
            "the family bound to epoch 0 is stale relative to the post-lock epoch 1");

        var family = await LoadFamilyAsync(first.TokenFamilyId);
        family.Should().ContainSingle().Which.IsRevoked.Should().BeFalse(
            "the rejection fails closed before any family mutation");
    }

    [Fact]
    public async Task Concurrent_ordinary_refreshes_serialize_and_produce_one_reuse_detection()
    {
        var account = await SeedAsync();
        var (first, token) = MakeSession(account.Id, epoch: 0);
        await AddSessionAsync(first);

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tryOne = AttemptRefreshAsync(_provider, token, startGate.Task);
        var tryTwo = AttemptRefreshAsync(_provider, token, startGate.Task);

        // Both handlers are in flight; release them together.
        startGate.TrySetResult();
        var results = await Task.WhenAll(tryOne, tryTwo);

        var winners = results.Where(r => r.Ok is not null).ToList();
        var reuseDetections = results.Where(r => r.Error is RefreshTokenReuseDetectedException).ToList();

        winners.Should().HaveCount(1, "the account-row lock serializes the two refreshes");
        reuseDetections.Should().HaveCount(1,
            "the losing refresh observes the consumed token and detects reuse");

        var family = await LoadFamilyAsync(first.TokenFamilyId);
        family.Should().OnlyContain(s => s.IsRevoked,
            "reuse revocation covers the whole family including the winner's replacement");
        family.Select(s => s.TokenFamilyId).Distinct().Should().ContainSingle();

        var events = await LoadEventsAsync(account.Id);
        events.Should().ContainSingle(e => e.EventType == "RefreshToken.ReuseDetected");

        (await LoadAccountAsync(account.Id))!.SessionRevocationEpoch.Should().Be(0,
            "ordinary rotation never advances the epoch");
    }

    [Fact]
    public async Task Refresh_of_a_session_bound_to_the_current_epoch_succeeds_and_chains()
    {
        var account = await SeedAsync();
        var (first, token) = MakeSession(account.Id, epoch: 0);
        await AddSessionAsync(first);

        var one = await RefreshAsync(_provider, token);
        var two = await RefreshAsync(_provider, one.RefreshToken);
        var three = await RefreshAsync(_provider, two.RefreshToken);

        var family = await LoadFamilyAsync(first.TokenFamilyId);
        family.Should().HaveCount(4);
        family.Should().OnlyContain(s => s.SessionRevocationEpochAtIssue == 0,
            "consecutive rotations stay bound to the unchanged current epoch");
        family.Should().ContainSingle(s =>
            s.RefreshTokenHash == TokenHasher.Hash(three.RefreshToken)
            && !s.RefreshTokenUsed
            && !s.IsRevoked);

        (await LoadAccountAsync(account.Id))!.SessionRevocationEpoch.Should().Be(0,
            "no emergency invalidation intervened, so the epoch never moved");
    }
}