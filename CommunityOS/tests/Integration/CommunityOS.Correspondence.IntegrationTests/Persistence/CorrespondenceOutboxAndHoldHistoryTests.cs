using CommunityOS.Correspondence.Application;
using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Domain.Events;
using CommunityOS.Correspondence.Domain.Exceptions;
using CommunityOS.Correspondence.Infrastructure;
using CommunityOS.Correspondence.Infrastructure.Persistence;
using FluentAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommunityOS.Correspondence.IntegrationTests.Persistence;

/// <summary>
/// Persistence proofs for the two Prompt 16B blocking findings, against a real
/// PostgreSQL instance with the production bus-outbox wiring (ADR-028
/// decision 8, ADR-015):
///
/// C-01 — every one of the five ratified lifecycle publications lands in the
/// transactional outbox in the same save as its letter mutation; a rolled-back
/// business transaction leaves no outbox row behind.
///
/// C-02 — hold placements and releases append their letter_status_history
/// rows atomically with the hold state (ADR-028 decisions 9F and 13).
///
/// NOTE: Docker is unavailable in the current environment, so this suite is
/// compile-only. It is executed in CI where a container runtime exists.
/// </summary>
public sealed class CorrespondenceOutboxAndHoldHistoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_correspondence")
        .WithUsername("communityos")
        .WithPassword("communityos")
        .Build();

    private ServiceProvider? _provider;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var connectionString = _postgres.GetConnectionString();

        await using var bootstrap = new CorrespondenceDbContext(
            new DbContextOptionsBuilder<CorrespondenceDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql
                    .MigrationsAssembly(typeof(CorrespondenceDbContext).Assembly.FullName))
                .Options);
        await bootstrap.Database.MigrateAsync();

        var services = new ServiceCollection();
        services.AddDbContext<CorrespondenceDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddMassTransit(bus =>
        {
            // Production wiring (EventBusServiceExtensions.ConfigureOutbox) on
            // the in-memory transport. The long query delay keeps the delivery
            // service idle so assertions observe persisted rows pre-delivery.
            bus.AddEntityFrameworkOutbox<CorrespondenceDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.QueryDelay = TimeSpan.FromMinutes(10);
                outbox.UseBusOutbox();
            });
            bus.UsingInMemory((_, _) => { });
        });

        _provider = services.BuildServiceProvider();

        // The bus is deliberately not started: with UseBusOutbox, publishes
        // are captured into the scoped DbContext before any transport
        // involvement, so no broker is needed to prove durable persistence.
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    private CorrespondenceDbContext CreateContext() =>
        _provider!.CreateScope().ServiceProvider.GetRequiredService<CorrespondenceDbContext>();

    private static LetterJournal CreateJournal(CorrespondenceDbContext db) =>
        new(db, NullLogger<LetterJournal>.Instance);

    private static Letter NewConfirmedLetter(Guid actor)
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "general", "S", "B",
            LetterSensitivity.Normal, Guid.NewGuid(), DateTime.UtcNow);
        letter.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
        letter.Confirm(actor, DateTime.UtcNow);
        return letter;
    }

    private static async Task<Letter> SubmitAsync(CorrespondenceDbContext db, IPublishEndpoint publishEndpoint)
    {
        var letter = NewConfirmedLetter(Guid.NewGuid());
        await CreateJournal(db).SubmitAsync(letter, "default", null,
            ct => publishEndpoint.Publish(new LetterSubmittedDomainEvent(
                letter.Id, letter.LetterYear!.Value, letter.LetterSequence!.Value,
                letter.OrganizationUnitId, letter.CategoryCode, "normal",
                letter.Recipients.Count,
                [.. letter.Recipients.Where(r => r.Kind == RecipientKind.Person).Select(r => r.PersonId!.Value)],
                [], letter.SubmittedBy!.Value), ct),
            CancellationToken.None);
        return letter;
    }

    /// <summary>Mirrors the fixed handler sequence: mutate → publish (buffered
    /// by the bus outbox into this context) → one atomic SaveChanges.</summary>
    private static Task PublishAsync(IPublishEndpoint publishEndpoint, object domainEvent) =>
        publishEndpoint.Publish(domainEvent);

    private static async Task<List<string>> OutboxMessageTypesAsync(CorrespondenceDbContext db)
    {
        var bodies = await db.Set<OutboxMessage>().Select(m => m.Body).ToListAsync();
        return bodies
            .Where(body => body.Contains("urn:message:CommunityOS.Contracts.Correspondence:"))
            .Select(body =>
            {
                var marker = "urn:message:CommunityOS.Contracts.Correspondence:";
                var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
                return body[start..].Split('"', ',')[0];
            })
            .OrderBy(name => name)
            .ToList();
    }

    [Fact]
    public async Task All_five_lifecycle_events_are_persisted_to_the_outbox_atomically()
    {
        await using var db = CreateContext();
        var journal = CreateJournal(db);
        await using var scope = _provider!.CreateAsyncScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var actor = Guid.NewGuid();

        // 1. LetterSubmitted — inside the submission transaction.
        var cancelled = await SubmitAsync(db, publishEndpoint);
        var dispatched = await SubmitAsync(db, publishEndpoint);
        var delivered = await SubmitAsync(db, publishEndpoint);
        var failed = await SubmitAsync(db, publishEndpoint);

        // 2. LetterCancelled.
        var (year, sequence) = (cancelled.LetterYear!.Value, cancelled.LetterSequence!.Value);
        cancelled.Cancel(actor, "superseded", DateTime.UtcNow);
        await PublishAsync(publishEndpoint, new LetterCancelledDomainEvent(
            cancelled.Id, year, sequence, cancelled.OrganizationUnitId,
            nameof(LetterStatus.Confirmed), "superseded", actor));
        await journal.SaveAsync(cancelled, CancellationToken.None);

        // 3. LetterDispatched.
        dispatched.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow);
        dispatched.RecordDispatch(DispatchLetterHandler.ManualMethodCode, actor, DateTime.UtcNow);
        await PublishAsync(publishEndpoint, new LetterDispatchedDomainEvent(
            dispatched.Id, dispatched.LetterYear!.Value, dispatched.LetterSequence!.Value,
            dispatched.OrganizationUnitId, DispatchLetterHandler.ManualMethodCode, actor));
        await journal.SaveAsync(dispatched, CancellationToken.None);

        // 4. LetterDeliveryConfirmed.
        delivered.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow);
        delivered.RecordDispatch(DispatchLetterHandler.ManualMethodCode, actor, DateTime.UtcNow);
        delivered.ConfirmDelivery(DispatchLetterHandler.ManualMethodCode, actor, DateTime.UtcNow);
        await PublishAsync(publishEndpoint, new LetterDeliveryConfirmedDomainEvent(
            delivered.Id, delivered.LetterYear!.Value, delivered.LetterSequence!.Value,
            delivered.OrganizationUnitId, DispatchLetterHandler.ManualMethodCode, actor));
        await journal.SaveAsync(delivered, CancellationToken.None);

        // 5. LetterDeliveryFailed.
        failed.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow);
        failed.RecordDispatch(DispatchLetterHandler.ManualMethodCode, actor, DateTime.UtcNow);
        failed.FailDelivery(DispatchLetterHandler.ManualMethodCode, "bad-address", actor, DateTime.UtcNow);
        await PublishAsync(publishEndpoint, new LetterDeliveryFailedDomainEvent(
            failed.Id, failed.LetterYear!.Value, failed.LetterSequence!.Value,
            failed.OrganizationUnitId, DispatchLetterHandler.ManualMethodCode, "bad-address", actor));
        await journal.SaveAsync(failed, CancellationToken.None);

        db.ChangeTracker.Clear();
        var types = await OutboxMessageTypesAsync(db);

        types.Should().BeEquivalentTo(
        [
            "LetterSubmitted", "LetterSubmitted", "LetterSubmitted", "LetterSubmitted",
            "LetterCancelled", "LetterDispatched", "LetterDeliveryConfirmed", "LetterDeliveryFailed"
        ]);

        // The mutations themselves committed with their events.
        await using var verify = new CorrespondenceDbContext(
            new DbContextOptionsBuilder<CorrespondenceDbContext>()
                .UseNpgsql(_postgres.GetConnectionString()).Options);
        (await verify.Letters.SingleAsync(l => l.Id == cancelled.Id)).Status
            .Should().Be(LetterStatus.Cancelled);
    }

    [Fact]
    public async Task A_rolled_back_transaction_leaves_no_outbox_row_and_no_mutation()
    {
        await using var db = CreateContext();
        var journal = CreateJournal(db);
        await using var scope = _provider!.CreateAsyncScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var baseline = await db.Set<OutboxMessage>().CountAsync();

        var letter = NewConfirmedLetter(Guid.NewGuid());
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Letters.Add(letter);
        letter.Cancel(Guid.NewGuid(), "other", DateTime.UtcNow);
        await PublishAsync(publishEndpoint, new LetterCancelledDomainEvent(
            letter.Id, 0, 0, letter.OrganizationUnitId,
            nameof(LetterStatus.Confirmed), "other", Guid.NewGuid()));
        await db.SaveChangesAsync();
        (await db.Set<OutboxMessage>().CountAsync()).Should().Be(baseline + 1);

        await tx.RollbackAsync();
        db.ChangeTracker.Clear();

        (await db.Set<OutboxMessage>().CountAsync()).Should().Be(baseline,
            "a failed business transaction discards the buffered event");
        db.Letters.Any(l => l.Id == letter.Id).Should().BeFalse("no mutation survives the rollback");
    }

    [Fact]
    public async Task Hold_placement_and_release_append_history_rows_atomically()
    {
        await using var db = CreateContext();
        var journal = CreateJournal(db);
        var placer = Guid.NewGuid();

        var first = NewConfirmedLetter(placer);
        var second = NewConfirmedLetter(placer);
        first.Submit(placer, 2026, 1, "default", null, DateTime.UtcNow);
        second.Submit(placer, 2026, 2, "default", null, DateTime.UtcNow);
        db.Letters.AddRange(first, second);
        await db.SaveChangesAsync();

        var legalHold = LetterHold.Create(first.Id, LetterHold.HoldTypeLegal, "investigation", placer, DateTime.UtcNow);
        var adminHold = LetterHold.Create(second.Id, LetterHold.HoldTypeAdministrative, "dispute", placer, DateTime.UtcNow);
        await journal.SaveHoldsAsync([legalHold, adminHold], CancellationToken.None);

        db.ChangeTracker.Clear();
        var placementHistory = await db.Set<LetterStatusHistory>()
            .Where(h => h.LetterId == first.Id || h.LetterId == second.Id)
            .OrderBy(h => h.OccurredOn)
            .ToListAsync();
        placementHistory.Should().HaveCount(2, "each hold placement appends exactly one history row");
        placementHistory.Should().OnlyContain(h =>
            h.Cause == HistoryCause.Command &&
            h.FromStatus == LetterStatus.Submitted &&
            h.ToStatus == LetterStatus.Submitted &&
            h.ActorId == placer &&
            (h.ReasonCode == "investigation" || h.ReasonCode == "dispute"),
            "placement rows carry no transition and no PII");

        var releaser = Guid.NewGuid();
        var releaseMoment = DateTime.UtcNow.AddMinutes(1);
        legalHold.Release(releaser, releaseMoment);
        await journal.SaveHoldReleaseAsync(legalHold, CancellationToken.None);

        db.ChangeTracker.Clear();
        var released = await db.LetterHolds.SingleAsync(h => h.Id == legalHold.Id);
        released.IsActive.Should().BeFalse();
        released.ReleasedBy.Should().Be(releaser);
        released.ReleasedOn.Should().Be(releaseMoment);

        var firstHistory = await db.Set<LetterStatusHistory>()
            .Where(h => h.LetterId == first.Id)
            .OrderBy(h => h.OccurredOn)
            .ToListAsync();
        firstHistory.Should().HaveCount(2);
        firstHistory.Last().Cause.Should().Be(HistoryCause.Command);
        firstHistory.Last().ActorId.Should().Be(releaser);
        firstHistory.Last().ReasonCode.Should().Be("investigation");
        firstHistory.Last().OccurredOn.Should().Be(releaseMoment);

        var secondHistory = await db.Set<LetterStatusHistory>()
            .Where(h => h.LetterId == second.Id)
            .ToListAsync();
        secondHistory.Should().HaveCount(1, "the unreleased hold's letter is untouched");

        // The active-hold invariant still blocks retention expiry for both.
        (await journal.ExistsActiveHoldAsync(first.Id, CancellationToken.None)).Should().BeFalse();
        (await journal.ExistsActiveHoldAsync(second.Id, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task A_failed_hold_batch_persists_neither_holds_nor_history_rows()
    {
        await using var db = CreateContext();
        var journal = CreateJournal(db);
        var placer = Guid.NewGuid();

        var held = NewConfirmedLetter(placer);
        held.Submit(placer, 2026, 1, "default", null, DateTime.UtcNow);
        db.Letters.Add(held);
        await db.SaveChangesAsync();

        var holdsBaseline = await db.LetterHolds.CountAsync();
        var historyBaseline = await db.Set<LetterStatusHistory>().CountAsync();

        Func<Task> act = () => journal.SaveHoldsAsync(
        [
            LetterHold.Create(held.Id, LetterHold.HoldTypeLegal, "legal-request", placer, DateTime.UtcNow),
            LetterHold.Create(Guid.NewGuid(), LetterHold.HoldTypeLegal, "other", placer, DateTime.UtcNow)
        ], CancellationToken.None);

        await act.Should().ThrowAsync<LetterNotFoundException>();

        db.ChangeTracker.Clear();
        (await db.LetterHolds.CountAsync()).Should().Be(holdsBaseline);
        (await db.Set<LetterStatusHistory>().CountAsync()).Should().Be(historyBaseline,
            "an unknown target fails the whole batch before any write");
    }
}

