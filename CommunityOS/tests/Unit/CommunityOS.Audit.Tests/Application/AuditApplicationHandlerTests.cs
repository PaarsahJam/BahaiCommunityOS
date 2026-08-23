using CommunityOS.Audit.Application;
using CommunityOS.Audit.Domain;
using CommunityOS.Audit.Domain.Exceptions;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Audit.Application.Permissions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CommunityOS.Audit.Tests.Application;

/// <summary>
/// Verifies the ratified visibility contract: coarse 403 gate, per-entry scope
/// filtering before pagination, sensitive entries absent without the second
/// pass, uniform 404 for missing/unauthorized single reads, hold atomicity,
/// and audit-of-audit journaling of exports/holds/purges.
/// </summary>
public sealed class AuditApplicationHandlerTests
{
    private static readonly AuditOptions Settings = new();

    private static IAuthorizationEvaluator Evaluator(Func<AuthorizationRequest, bool> allows)
    {
        var evaluator = Substitute.For<IAuthorizationEvaluator>();
        AuthorizationDecision Decide(AuthorizationRequest request) => allows(request)
            ? AuthorizationDecision.Allow("grant", ["grant"], DateTime.UtcNow)
            : AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.ScopeMismatch, DateTime.UtcNow);
        evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Decide(callInfo.Arg<AuthorizationRequest>()));
        evaluator.EvaluateBatchAsync(Arg.Any<IReadOnlyList<AuthorizationRequest>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<IReadOnlyList<AuthorizationRequest>>().Select(Decide).ToList());
        return evaluator;
    }

    private static AuditEntryRow Row(
        Guid? unit = null,
        bool sensitive = false,
        string sourceService = "records") =>
        new(Guid.NewGuid(), sourceService, "RecordVerified", "record-verified", null,
            "record", Guid.NewGuid(), null, null, null, unit,
            sensitive ? AuditSensitivity.Sensitive : AuditSensitivity.Normal, null,
            "standard-7y", DateTime.UtcNow.AddYears(7), DateTime.UtcNow, DateTime.UtcNow);

    private static AuditQueryFilters NoFilters() =>
        new(null, null, null, null, null, null, null, null, null, null);

    [Fact]
    public async Task Query_without_any_read_grant_is_a_coarse_403()
    {
        var actor = Guid.NewGuid();
        var reader = Substitute.For<IAuditReader>();
        var evaluator = Evaluator(_ => false);
        var handler = new AuditQueryHandler(reader, new AuthorizationGuard(evaluator), evaluator,
            Microsoft.Extensions.Options.Options.Create(Settings));

        var action = () => handler.Handle(
            new AuditQuery(actor, NoFilters(), "desc", false, 10, 0), CancellationToken.None);

        await action.Should().ThrowAsync<AuthorizationForbiddenException>();
        await reader.DidNotReceiveWithAnyArgs().QueryAsync(default!, default, default, default, default);
    }

    [Fact]
    public async Task Query_filters_rows_at_the_scope_boundary_before_pagination()
    {
        var actor = Guid.NewGuid();
        var visibleUnit = Guid.NewGuid();
        var hiddenUnit = Guid.NewGuid();

        var rows = new List<AuditEntryRow>
        {
            Row(visibleUnit), Row(hiddenUnit), Row(unit: null)
        };
        var reader = Substitute.For<IAuditReader>();
        reader.QueryAsync(Arg.Any<AuditQueryFilters>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var offset = callInfo.ArgAt<int>(2);
                return rows.Skip(offset).Take(callInfo.ArgAt<int>(3)).ToList();
            });

        // Coarse-gate contexts never carry a resource id, so this grants the
        // gate plus exactly one unit's rows; unscoped rows resolve to a
        // resource context that stays denied.
        var evaluator = Evaluator(r =>
            r.Permission == AuditPermissions.EntryRead && r.Context.ResourceId is null ||
            r.Context.OrganizationUnitId == visibleUnit);
        var handler = new AuditQueryHandler(reader, new AuthorizationGuard(evaluator), evaluator,
            Microsoft.Extensions.Options.Options.Create(Settings));

        var page = await handler.Handle(
            new AuditQuery(actor, NoFilters(), "desc", false, 10, 0), CancellationToken.None);

        page.Returned.Should().Be(1);
        page.Items.Single().OrganizationUnitId.Should().Be(visibleUnit);
    }

    [Fact]
    public async Task Sensitive_entries_are_absent_unless_the_second_pass_allows()
    {
        var actor = Guid.NewGuid();
        var normal = Row();
        var sensitive = Row(sensitive: true);
        var rows = new List<AuditEntryRow> { normal, sensitive };

        var reader = Substitute.For<IAuditReader>();
        reader.QueryAsync(Arg.Any<AuditQueryFilters>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(rows);

        // Base read everywhere, but the sensitive second pass is held by
        // nobody: includeSensitive=true is rejected while the standard page
        // silently drops the sensitive row.
        var evaluator = Evaluator(r => r.Permission == AuditPermissions.EntryRead);
        var handler = new AuditQueryHandler(reader, new AuthorizationGuard(evaluator), evaluator,
            Microsoft.Extensions.Options.Options.Create(Settings));

        // includeSensitive=true requires audit.entry.read.sensitive — the
        // coarse gate rejects it when only the base read grant exists.
        await FluentActions.Invoking(() => handler.Handle(
                new AuditQuery(actor, NoFilters(), "desc", true, 10, 0), CancellationToken.None))
            .Should().ThrowAsync<AuthorizationForbiddenException>();

        var standardPage = await handler.Handle(
            new AuditQuery(actor, NoFilters(), "desc", false, 10, 0), CancellationToken.None);
        standardPage.Items.Should().ContainSingle(i => i.Id == normal.Id);
        standardPage.Items.Should().NotContain(i => i.Id == sensitive.Id);
    }

    [Fact]
    public async Task Single_read_returns_404_for_missing_and_unauthorized_alike()
    {
        var actor = Guid.NewGuid();
        var hidden = Row(unit: Guid.NewGuid());
        var unauthorizedId = Guid.NewGuid();

        var reader = Substitute.For<IAuditReader>();
        reader.FindAsync(unauthorizedId, Arg.Any<CancellationToken>())
            .Returns((AuditEntryRow?)null);
        reader.FindAsync(hidden.Id, Arg.Any<CancellationToken>())
            .Returns(hidden);

        // The coarse capability gate is a global-context check; the hidden
        // row's resource context never matches, so both reads are uniform 404.
        var evaluator = Evaluator(r => r.Permission == AuditPermissions.EntryRead && r.Context.IsGlobal);
        var handler = new GetAuditEntryHandler(reader, new AuthorizationGuard(evaluator), evaluator);

        await FluentActions.Invoking(() => handler.Handle(new GetAuditEntry(actor, unauthorizedId), CancellationToken.None))
            .Should().ThrowAsync<AuditEntryNotFoundException>();
        await FluentActions.Invoking(() => handler.Handle(new GetAuditEntry(actor, hidden.Id), CancellationToken.None))
            .Should().ThrowAsync<AuditEntryNotFoundException>();
    }

    [Fact]
    public async Task Export_journals_an_audit_of_audit_entry_with_counts_only()
    {
        var actor = Guid.NewGuid();
        var row = Row();
        var reader = Substitute.For<IAuditReader>();
        reader.QueryAsync(Arg.Any<AuditQueryFilters>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([row]);
        var journal = Substitute.For<IAuditJournal>();

        var evaluator = Evaluator(_ => true);
        var handler = new ExportAuditHandler(reader, new AuthorizationGuard(evaluator), evaluator, journal,
            Microsoft.Extensions.Options.Options.Create(Settings));

        var result = await handler.Handle(
            new ExportAuditCommand(actor, "ndjson", NoFilters(), false, null), CancellationToken.None);

        result.RowCount.Should().Be(1);
        result.Format.Should().Be("ndjson");
        await journal.Received(1).AppendAsync(
            Arg.Is<AuditEntry>(e => e.SourceService == "audit"
                && e.SourceEventType == "AuditEntriesExported"
                && e.RetentionClass == Settings.JournalClass),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Hold_placement_fails_atomically_when_any_target_is_invisible()
    {
        var actor = Guid.NewGuid();
        var target = Row();
        var unknownId = Guid.NewGuid();

        var reader = Substitute.For<IAuditReader>();
        reader.FindRangeAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([target]);
        reader.HasActiveHoldAsync(target.Id, Arg.Any<CancellationToken>()).Returns(false);
        var journal = Substitute.For<IAuditJournal>();

        // Global grants only: the scoped target is invisible.
        var evaluator = Evaluator(r => r.Context.OrganizationUnitId is null && r.Context.ResourceId is null);
        var handler = new PlaceHoldHandler(reader, new AuthorizationGuard(evaluator), evaluator, journal,
            Microsoft.Extensions.Options.Options.Create(Settings));

        await FluentActions.Invoking(() => handler.Handle(
                new PlaceHoldCommand(actor, [target.Id, unknownId], "legal", "dispute"), CancellationToken.None))
            .Should().ThrowAsync<AuditEntryNotFoundException>();

        await journal.DidNotReceiveWithAnyArgs().AppendWithHoldsAsync(default!, default!, default);
    }

    [Fact]
    public async Task Hold_placement_conflicts_with_an_existing_active_hold()
    {
        var actor = Guid.NewGuid();
        var target = Row();

        var reader = Substitute.For<IAuditReader>();
        reader.FindRangeAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([target]);
        reader.HasActiveHoldAsync(target.Id, Arg.Any<CancellationToken>()).Returns(true);
        var journal = Substitute.For<IAuditJournal>();

        var evaluator = Evaluator(_ => true);
        var handler = new PlaceHoldHandler(reader, new AuthorizationGuard(evaluator), evaluator, journal,
            Microsoft.Extensions.Options.Options.Create(Settings));

        await FluentActions.Invoking(() => handler.Handle(
                new PlaceHoldCommand(actor, [target.Id], "legal", "legal-request"), CancellationToken.None))
            .Should().ThrowAsync<AuditConflictException>()
            .WithMessage("*active hold*");
    }

    [Fact]
    public async Task Releasing_an_already_released_hold_is_a_conflict()
    {
        var actor = Guid.NewGuid();
        var hold = AuditEntryHold.Create(Guid.NewGuid(), AuditEntryHold.Administrative, "other", actor, DateTime.UtcNow);
        hold.Release(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5));

        var reader = Substitute.For<IAuditReader>();
        reader.FindHoldAsync(hold.Id, Arg.Any<CancellationToken>()).Returns(hold);
        var journal = Substitute.For<IAuditJournal>();
        var evaluator = Evaluator(_ => true);

        var handler = new ReleaseHoldHandler(reader, new AuthorizationGuard(evaluator), journal,
            Microsoft.Extensions.Options.Options.Create(Settings));

        await FluentActions.Invoking(() => handler.Handle(new ReleaseHoldCommand(actor, hold.Id), CancellationToken.None))
            .Should().ThrowAsync<AuditConflictException>();
    }

    [Fact]
    public async Task Purge_executes_one_batch_and_reports_the_marker()
    {
        var actor = Guid.NewGuid();
        var journal = Substitute.For<IAuditJournal>();
        AuditEntry? marker = null;
        journal.PurgeExpiredBatchAsync(
                Arg.Any<int>(),
                Arg.Do<Func<int, IReadOnlyList<string>, AuditEntry>>(f => marker = f(3, ["default"])),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new PurgeBatchResult(3, 12));

        var evaluator = Evaluator(_ => true);
        var handler = new PurgeExpiredHandler(journal, new AuthorizationGuard(evaluator),
            Microsoft.Extensions.Options.Options.Create(Settings));

        var result = await handler.Handle(new PurgeExpiredCommand(actor, 0), CancellationToken.None);

        result.PurgedCount.Should().Be(3);
        result.RemainingExpiredEstimate.Should().Be(12);
        marker.Should().NotBeNull();
        marker!.SourceEventType.Should().Be("AuditEntriesPurged");
        marker.SourceService.Should().Be("audit");
    }
}
