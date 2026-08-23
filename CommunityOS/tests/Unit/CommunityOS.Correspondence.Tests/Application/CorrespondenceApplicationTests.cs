using CommunityOS.Correspondence.Application;
using CommunityOS.Correspondence.Application.Authorization;
using CommunityOS.Correspondence.Application.Permissions;
using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Domain.Events;
using CommunityOS.Correspondence.Domain.Exceptions;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Options;
using Xunit;

namespace CommunityOS.Correspondence.Tests.Application;

/// <summary>Fail-closed stub evaluator: allows exactly the listed
/// (subject, permission) pairs; everything else is denied.</summary>
internal sealed class StubEvaluator(params (Guid Subject, string Permission)[] allowed) : IAuthorizationEvaluator
{
    public Task<AuthorizationDecision> EvaluateAsync(AuthorizationRequest request, CancellationToken ct = default) =>
        Task.FromResult(allowed.Any(a => a.Subject == request.SubjectId && a.Permission == request.Permission)
            ? AuthorizationDecision.Allow("d", ["p"], DateTime.UtcNow)
            : AuthorizationDecision.Deny("d", AuthorizationDecisionReason.DeniedByDefault, DateTime.UtcNow));

    public async Task<IReadOnlyList<AuthorizationDecision>> EvaluateBatchAsync(
        IReadOnlyList<AuthorizationRequest> requests, CancellationToken ct = default) =>
        [.. await Task.WhenAll(requests.Select(r => EvaluateAsync(r, ct)))];
}

internal sealed class StubJournal(Letter? letter = null) : ILetterJournal
{
    public Letter? Tracked { get; private set; } = letter;

    public Task<Letter?> FindTrackedLetterAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Tracked?.Id == id ? Tracked : null);

    public List<Letter> Saved { get; } = [];

    public Task SaveAsync(Letter letter, CancellationToken ct)
    {
        Saved.Add(letter);
        return Task.CompletedTask;
    }

    public Task FlushOutboxAsync(CancellationToken ct) => Task.CompletedTask;

    public Func<CancellationToken, Task>? SubmittedCallback { get; private set; }

    public Task SubmitAsync(
        Letter letter, string retentionClass, DateTime? retentionExpiresOn,
        Func<CancellationToken, Task> publishSubmittedEvent, CancellationToken ct)
    {
        letter.Submit(letter.CreatedBy, 2026, 1, retentionClass, retentionExpiresOn, DateTime.UtcNow);
        SubmittedCallback = publishSubmittedEvent;
        return Task.CompletedTask;
    }

    public Task SaveTemplateAsync(Template letterTemplate, CancellationToken ct) => Task.CompletedTask;
    public Task<bool> ExistsActiveHoldAsync(Guid letterId, CancellationToken ct) => Task.FromResult(false);
    public Task SaveHoldsAsync(IReadOnlyList<LetterHold> holds, CancellationToken ct) => Task.CompletedTask;
    public Task SaveHoldReleaseAsync(LetterHold hold, CancellationToken ct) => Task.CompletedTask;
    public Task SaveExportActivityAsync(ExportActivity activity, CancellationToken ct) => Task.CompletedTask;
    public Task<PurgeBatchResult> PurgeExpiredBatchAsync(int maxBatchSize, DateTime asOf, CancellationToken ct) =>
        Task.FromResult(new PurgeBatchResult(0, 0));
}

internal sealed class CapturingPublisher : IPublisher
{
    public List<object> Published { get; } = [];

    public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : INotification
    {
        Published.Add(notification!);
        return Task.CompletedTask;
    }

    public Task Publish(object notification, CancellationToken ct = default) =>
        throw new NotSupportedException("Object overloads are not part of the correspondence flows.");
}

internal static class CorrespondenceTestOptions
{
    public static IOptions<CorrespondenceOptions> Create() =>
        Options.Create(new CorrespondenceOptions());
}

public sealed class LetterVisibilityTests
{
    private static LetterSummaryRow Row(bool sensitive = false) => new(
        Guid.NewGuid(), Guid.NewGuid(), "general", 2026, 1,
        "materialized", sensitive ? "sensitive" : "normal", false,
        1, 1, 0, 0, null, DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task Normal_letters_need_only_the_read_capability()
    {
        var actor = Guid.NewGuid();
        var row = Row();
        var evaluator = new StubEvaluator((actor, LetterPermissions.LetterRead));

        (await LetterVisibility.IsVisibleAsync(evaluator, actor, row, includeSensitive: false, CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Missing_read_capability_hides_the_letter()
    {
        var row = Row();
        (await LetterVisibility.IsVisibleAsync(new StubEvaluator(), Guid.NewGuid(), row, false, CancellationToken.None))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Sensitive_letters_require_the_second_pass()
    {
        var actor = Guid.NewGuid();
        var row = Row(sensitive: true);

        var readOnly = new StubEvaluator((actor, LetterPermissions.LetterRead));
        (await LetterVisibility.IsVisibleAsync(readOnly, actor, row, includeSensitive: true, CancellationToken.None))
            .Should().BeFalse("the second pass is mandatory for sensitive letters");

        var both = new StubEvaluator(
            (actor, LetterPermissions.LetterRead), (actor, LetterPermissions.LetterReadSensitive));
        (await LetterVisibility.IsVisibleAsync(both, actor, row, true, CancellationToken.None))
            .Should().BeTrue();

        // Opting out of the sensitive pass hides the row even when capable.
        (await LetterVisibility.IsVisibleAsync(both, actor, row, false, CancellationToken.None))
            .Should().BeFalse();
    }
}

public sealed class LetterOperationGateTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    private static Letter NewLetter(bool sensitive = false) =>
        Letter.CreateDraft(Guid.NewGuid(), "general", "S", "B",
            sensitive ? LetterSensitivity.Sensitive : LetterSensitivity.Normal,
            Guid.NewGuid(), DateTime.UtcNow);

    [Fact]
    public async Task Unauthorized_or_missing_letters_are_uniformly_404()
    {
        var letter = NewLetter();
        var journal = new StubJournal(letter);

        // No capabilities at all → indistinguishable from missing.
        await FluentActions.Awaiting(() => LetterOperationGate.RequireAccessibleLetterAsync(
                journal, new StubEvaluator(), Actor, LetterPermissions.LetterUpdate, letter.Id, CancellationToken.None))
            .Should().ThrowAsync<LetterNotFoundException>();

        // Unknown id → same exception type.
        await FluentActions.Awaiting(() => LetterOperationGate.RequireAccessibleLetterAsync(
                journal, new StubEvaluator(), Actor, LetterPermissions.LetterUpdate, Guid.NewGuid(), CancellationToken.None))
            .Should().ThrowAsync<LetterNotFoundException>();
    }

    [Fact]
    public async Task Sensitive_letters_require_read_and_second_pass_before_the_action()
    {
        var letter = NewLetter(sensitive: true);
        var journal = new StubJournal(letter);

        var fullAccess = new StubEvaluator(
            (Actor, LetterPermissions.LetterRead),
            (Actor, LetterPermissions.LetterReadSensitive),
            (Actor, LetterPermissions.LetterSubmit));
        (await LetterOperationGate.RequireAccessibleLetterAsync(
            journal, fullAccess, Actor, LetterPermissions.LetterSubmit, letter.Id, CancellationToken.None))
            .Id.Should().Be(letter.Id);

        var missingSecondPass = new StubEvaluator(
            (Actor, LetterPermissions.LetterRead),
            (Actor, LetterPermissions.LetterSubmit));
        await FluentActions.Awaiting(() => LetterOperationGate.RequireAccessibleLetterAsync(
                journal, missingSecondPass, Actor, LetterPermissions.LetterSubmit, letter.Id, CancellationToken.None))
            .Should().ThrowAsync<LetterNotFoundException>("admin capability does not imply read");
    }

    private static class FluentActions
    {
        public static Func<Task<T>> Awaiting<T>(Func<Task<T>> action) => action;
    }
}

public sealed class PlaceHoldsTests
{
    private sealed class HoldingReader(IReadOnlyList<LetterSummaryRow> rows, HashSet<Guid> held) : ILetterReader
    {
        public Task<IReadOnlyList<LetterSummaryRow>> QueryAsync(
            LetterQueryFilters filters, bool ascending, int offset, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LetterSummaryRow>>([]);

        public Task<LetterSummaryRow?> FindSummaryAsync(Guid id, CancellationToken ct) => Task.FromResult<LetterSummaryRow?>(null);

        public Task<LetterDetailRow?> FindDetailAsync(Guid id, CancellationToken ct) => Task.FromResult<LetterDetailRow?>(null);

        public Task<IReadOnlyList<LetterSummaryRow>> FindRangeSummariesAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LetterSummaryRow>>([.. rows.Where(r => ids.Contains(r.Id))]);

        public Task<IReadOnlyList<HistoryRow>> GetHistoryAsync(Guid letterId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HistoryRow>>([]);

        public Task<bool> HasActiveHoldAsync(Guid letterId, CancellationToken ct) => Task.FromResult(held.Contains(letterId));

        public Task<LetterHold?> FindHoldAsync(Guid holdId, CancellationToken ct) => Task.FromResult<LetterHold?>(null);

        public Task<int> CountExpiredUnheldAsync(DateTime asOf, CancellationToken ct) => Task.FromResult(0);

        public Task<IReadOnlyList<TemplateRow>> ListActiveTemplatesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TemplateRow>>([]);

        public Task<Template?> FindTemplateAsync(Guid id, CancellationToken ct) => Task.FromResult<Template?>(null);

        public Task<bool> TemplateCodeExistsAsync(string code, CancellationToken ct) => Task.FromResult(false);

        public Task<IReadOnlyList<StuckSubmissionRow>> ListStuckSubmissionsAsync(DateTime olderThan, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StuckSubmissionRow>>([]);
    }

    private static LetterSummaryRow Row() => new(
        Guid.NewGuid(), Guid.NewGuid(), "general", null, null, "draft", "normal", false,
        1, 1, 0, 0, null, DateTime.UtcNow, null);

    [Fact]
    public async Task Unknown_or_invisible_targets_fail_the_whole_batch_as_404()
    {
        var visible = Row();
        var actor = Guid.NewGuid();
        var evaluator = new StubEvaluator(
            (actor, LetterPermissions.LetterAdmin), (actor, LetterPermissions.LetterRead));
        var reader = new HoldingReader([visible], []);
        var journal = new StubJournal();

        var handler = new PlaceHoldsHandler(reader, journal,
            new AuthorizationGuard(evaluator), evaluator, CorrespondenceTestOptions.Create());

        // One unknown id among two: uniform 404 without disclosure.
        Func<Task> act404 = () => handler.Handle(
            new PlaceHoldsCommand(actor, [visible.Id, Guid.NewGuid()], LetterHold.HoldTypeLegal, "investigation"),
            CancellationToken.None);
        await act404.Should().ThrowAsync<LetterNotFoundException>();
    }

    [Fact]
    public async Task An_active_hold_on_any_target_conflicts_the_whole_batch()
    {
        var a = Row();
        var b = Row();
        var actor = Guid.NewGuid();
        var evaluator = new StubEvaluator(
            (actor, LetterPermissions.LetterAdmin), (actor, LetterPermissions.LetterRead));
        var reader = new HoldingReader([a, b], [b.Id]);
        var handler = new PlaceHoldsHandler(reader, new StubJournal(),
            new AuthorizationGuard(evaluator), evaluator, CorrespondenceTestOptions.Create());

        Func<Task> actConflict = () => handler.Handle(
            new PlaceHoldsCommand(actor, [a.Id, b.Id], LetterHold.HoldTypeAdministrative, "dispute"),
            CancellationToken.None);
        await actConflict.Should().ThrowAsync<LetterConflictException>();
    }

    [Fact]
    public async Task Oversized_batches_are_rejected_without_store_calls()
    {
        var actor = Guid.NewGuid();
        var evaluator = new StubEvaluator((actor, LetterPermissions.LetterAdmin));
        var reader = new HoldingReader([], []);
        var options = Options.Create(new CorrespondenceOptions { HoldMaxBatchSize = 2 });
        var handler = new PlaceHoldsHandler(reader, new StubJournal(),
            new AuthorizationGuard(evaluator), evaluator, options);

        Func<Task> actOversize = () => handler.Handle(
            new PlaceHoldsCommand(actor, [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                LetterHold.HoldTypeLegal, "investigation"),
            CancellationToken.None);
        await actOversize.Should().ThrowAsync<ArgumentException>();
    }
}

public sealed class ReconcileMaterializationsTests
{
    private sealed class StuckReader(IReadOnlyList<StuckSubmissionRow> rows) : ILetterReader
    {
        public Task<IReadOnlyList<LetterSummaryRow>> QueryAsync(
            LetterQueryFilters filters, bool ascending, int offset, int maxRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LetterSummaryRow>>([]);

        public Task<LetterSummaryRow?> FindSummaryAsync(Guid id, CancellationToken ct) => Task.FromResult<LetterSummaryRow?>(null);
        public Task<LetterDetailRow?> FindDetailAsync(Guid id, CancellationToken ct) => Task.FromResult<LetterDetailRow?>(null);

        public Task<IReadOnlyList<LetterSummaryRow>> FindRangeSummariesAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LetterSummaryRow>>([]);

        public Task<IReadOnlyList<HistoryRow>> GetHistoryAsync(Guid letterId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HistoryRow>>([]);

        public Task<bool> HasActiveHoldAsync(Guid letterId, CancellationToken ct) => Task.FromResult(false);
        public Task<LetterHold?> FindHoldAsync(Guid holdId, CancellationToken ct) => Task.FromResult<LetterHold?>(null);
        public Task<int> CountExpiredUnheldAsync(DateTime asOf, CancellationToken ct) => Task.FromResult(0);
        public Task<IReadOnlyList<TemplateRow>> ListActiveTemplatesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TemplateRow>>([]);
        public Task<Template?> FindTemplateAsync(Guid id, CancellationToken ct) => Task.FromResult<Template?>(null);
        public Task<bool> TemplateCodeExistsAsync(string code, CancellationToken ct) => Task.FromResult(false);

        public Task<IReadOnlyList<StuckSubmissionRow>> ListStuckSubmissionsAsync(DateTime olderThan, int maxRows, CancellationToken ct) =>
            Task.FromResult(rows);
    }

    private static Letter SubmittedLetter()
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "general", "S", "B",
            LetterSensitivity.Normal, Guid.NewGuid(), DateTime.UtcNow);
        letter.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
        letter.Confirm(Guid.NewGuid(), DateTime.UtcNow);
        letter.Submit(letter.SubmittedBy ?? Guid.NewGuid(), 2026, 1, "default", null, DateTime.UtcNow);
        return letter;
    }

    [Fact]
    public async Task Only_still_submitted_letters_get_verbatim_republished()
    {
        var submitted = SubmittedLetter();
        var materialized = SubmittedLetter();
        materialized.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow);

        var stuckIds = new[] { submitted.Id, materialized.Id };
        var journal = new StubJournal();
        var publisher = new CapturingPublisher();
        var actor = Guid.NewGuid();
        var evaluator = new StubEvaluator((actor, LetterPermissions.LetterAdmin));
        var handler = new ReconcileMaterializationsHandler(
            new StuckReader(stuckIds.Select(id => new StuckSubmissionRow(id, Guid.NewGuid(), DateTime.UtcNow.AddHours(-2))).ToList()),
            new MultiLetterJournal(journal, submitted, materialized),
            new AuthorizationGuard(evaluator),
            publisher,
            CorrespondenceTestOptions.Create());

        var result = await handler.Handle(new ReconcileMaterializationsCommand(actor), CancellationToken.None);

        result.Inspected.Should().Be(2);
        result.Republished.Should().Be(1, "already-materialized letters must not be re-published");
        publisher.Published.Should().ContainSingle().Which.Should().BeOfType<LetterSubmittedDomainEvent>();
    }

    private sealed class MultiLetterJournal(StubJournal inner, params Letter[] letters) : ILetterJournal
    {
        public Task<Letter?> FindTrackedLetterAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(letters.FirstOrDefault(l => l.Id == id));

        public Task SaveAsync(Letter letter, CancellationToken ct) => inner.SaveAsync(letter, ct);
        public Task FlushOutboxAsync(CancellationToken ct) => Task.CompletedTask;

        public Task SubmitAsync(
            Letter letter, string retentionClass, DateTime? retentionExpiresOn,
            Func<CancellationToken, Task> publishSubmittedEvent, CancellationToken ct) =>
            inner.SubmitAsync(letter, retentionClass, retentionExpiresOn, publishSubmittedEvent, ct);

        public Task SaveTemplateAsync(Template letterTemplate, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsActiveHoldAsync(Guid letterId, CancellationToken ct) => Task.FromResult(false);
        public Task SaveHoldsAsync(IReadOnlyList<LetterHold> holds, CancellationToken ct) => Task.CompletedTask;
        public Task SaveHoldReleaseAsync(LetterHold hold, CancellationToken ct) => Task.CompletedTask;
        public Task SaveExportActivityAsync(ExportActivity activity, CancellationToken ct) => Task.CompletedTask;
        public Task<PurgeBatchResult> PurgeExpiredBatchAsync(int maxBatchSize, DateTime asOf, CancellationToken ct) =>
            Task.FromResult(new PurgeBatchResult(0, 0));
    }
}
