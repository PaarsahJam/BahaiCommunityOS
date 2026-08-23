using CommunityOS.Correspondence.Application;
using CommunityOS.Correspondence.Application.Authorization;
using CommunityOS.Correspondence.Application.Permissions;
using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Domain.Events;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CommunityOS.Correspondence.Tests.Application;

/// <summary>
/// Proves the outbox atomicity contract of the lifecycle command handlers
/// (ADR-028 decision 8): every integration-event publication happens BEFORE
/// the single journal save that commits letter mutation, history append and
/// bus-outbox rows together — so a failed save discards the buffered event
/// and no publication-after-SaveChanges gap can silently lose events.
/// </summary>
public sealed class LifecycleOutboxOrderingTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    private static Letter NewConfirmedLetter()
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "general", "S", "B",
            LetterSensitivity.Normal, Guid.NewGuid(), DateTime.UtcNow);
        letter.AddRecipient(RecipientKind.Person, Guid.NewGuid(), null, null, DateTime.UtcNow);
        letter.Confirm(Actor, DateTime.UtcNow);
        return letter;
    }

    private static Letter NewSubmittedLetter()
    {
        var letter = NewConfirmedLetter();
        letter.Submit(Actor, 2026, 7, "default", null, DateTime.UtcNow);
        return letter;
    }

    private static Letter NewMaterializedLetter()
    {
        var letter = NewSubmittedLetter();
        letter.MarkMaterialized(Guid.NewGuid(), 1, "hash", DateTime.UtcNow);
        return letter;
    }

    private static Letter NewDispatchedLetter()
    {
        var letter = NewMaterializedLetter();
        letter.RecordDispatch(DispatchLetterHandler.ManualMethodCode, Actor, DateTime.UtcNow);
        return letter;
    }

    [Fact]
    public async Task Cancellation_is_published_before_its_single_atomic_save()
    {
        var letter = NewConfirmedLetter();
        var log = new List<string>();
        var journal = new SequencedJournal(letter, log);
        var publisher = new RecordingPublisher(log);
        var evaluator = new StubEvaluator((Actor, LetterPermissions.LetterRead), (Actor, LetterPermissions.LetterCancel));
        var handler = new CancelLetterHandler(
            journal, new AuthorizationGuard(evaluator), evaluator, publisher);

        await handler.Handle(
            new CancelLetterCommand(Actor, letter.Id, "superseded"), CancellationToken.None);

        log.Should().Equal("publish:LetterCancelledDomainEvent", "save");
        journal.SaveCount.Should().Be(1);
        publisher.Published.Should().ContainSingle().Which
            .Should().BeOfType<LetterCancelledDomainEvent>().Which
            .CancelledFromStatus.Should().Be("confirmed", "the pre-cancel status is captured before mutation");
        letter.Status.Should().Be(LetterStatus.Cancelled);
    }

    [Fact]
    public async Task Dispatch_is_published_before_its_single_atomic_save()
    {
        var letter = NewMaterializedLetter();
        var log = new List<string>();
        var journal = new SequencedJournal(letter, log);
        var publisher = new RecordingPublisher(log);
        var evaluator = new StubEvaluator((Actor, LetterPermissions.LetterRead), (Actor, LetterPermissions.LetterAdmin));
        var handler = new DispatchLetterHandler(
            journal, new AuthorizationGuard(evaluator), evaluator, publisher);

        await handler.Handle(
            new DispatchLetterCommand(Actor, letter.Id, DispatchLetterHandler.ManualMethodCode),
            CancellationToken.None);

        log.Should().Equal("publish:LetterDispatchedDomainEvent", "save");
        journal.SaveCount.Should().Be(1);
        publisher.Published.Should().ContainSingle().Which
            .Should().BeOfType<LetterDispatchedDomainEvent>().Which
            .MethodCode.Should().Be(DispatchLetterHandler.ManualMethodCode);
        letter.Status.Should().Be(LetterStatus.Dispatched);
    }

    [Fact]
    public async Task Delivery_confirmation_is_published_before_its_single_atomic_save()
    {
        var letter = NewDispatchedLetter();
        var log = new List<string>();
        var journal = new SequencedJournal(letter, log);
        var publisher = new RecordingPublisher(log);
        var evaluator = new StubEvaluator((Actor, LetterPermissions.LetterRead), (Actor, LetterPermissions.LetterAdmin));
        var handler = new RecordDeliveryOutcomeHandler(
            journal, new AuthorizationGuard(evaluator), evaluator, publisher);

        await handler.Handle(
            new RecordDeliveryOutcomeCommand(Actor, letter.Id, "confirmed", null),
            CancellationToken.None);

        log.Should().Equal("publish:LetterDeliveryConfirmedDomainEvent", "save");
        journal.SaveCount.Should().Be(1);
        publisher.Published.Should().ContainSingle().Which
            .Should().BeOfType<LetterDeliveryConfirmedDomainEvent>();
        letter.Status.Should().Be(LetterStatus.Delivered);
    }

    [Fact]
    public async Task Delivery_failure_is_published_before_its_single_atomic_save()
    {
        var letter = NewDispatchedLetter();
        var log = new List<string>();
        var journal = new SequencedJournal(letter, log);
        var publisher = new RecordingPublisher(log);
        var evaluator = new StubEvaluator((Actor, LetterPermissions.LetterRead), (Actor, LetterPermissions.LetterAdmin));
        var handler = new RecordDeliveryOutcomeHandler(
            journal, new AuthorizationGuard(evaluator), evaluator, publisher);

        await handler.Handle(
            new RecordDeliveryOutcomeCommand(Actor, letter.Id, "failed", "bad-address"),
            CancellationToken.None);

        log.Should().Equal("publish:LetterDeliveryFailedDomainEvent", "save");
        journal.SaveCount.Should().Be(1);
        publisher.Published.Should().ContainSingle().Which
            .Should().BeOfType<LetterDeliveryFailedDomainEvent>().Which
            .ReasonCode.Should().Be("bad-address");
        letter.Status.Should().Be(LetterStatus.DeliveryFailed);
    }

    [Fact]
    public async Task A_failed_save_propagates_and_leaves_only_the_uncommitted_buffer()
    {
        var letter = NewConfirmedLetter();
        var log = new List<string>();
        var journal = new FailingSaveJournal(letter, log);
        var publisher = new RecordingPublisher(log);
        var evaluator = new StubEvaluator((Actor, LetterPermissions.LetterRead), (Actor, LetterPermissions.LetterCancel));
        var handler = new CancelLetterHandler(
            journal, new AuthorizationGuard(evaluator), evaluator, publisher);

        Func<Task> act = () => handler.Handle(
            new CancelLetterCommand(Actor, letter.Id, "superseded"), CancellationToken.None);

        // The single SaveChanges is the atomicity boundary: it fails, so the
        // letter change, history row and buffered outbox row are discarded
        // together by the context (ADR-015 bus-outbox semantics).
        await act.Should().ThrowAsync<DbUpdateException>();
        log.Should().Equal("publish:LetterCancelledDomainEvent", "save-failed");
        journal.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Submission_still_flushes_LetterSubmitted_inside_the_submission_transaction()
    {
        var letter = NewConfirmedLetter();
        var journal = new SequencedJournal(letter, []);
        var publisher = new RecordingPublisher([]);
        var evaluator = new StubEvaluator((Actor, LetterPermissions.LetterRead), (Actor, LetterPermissions.LetterSubmit));

        var policy = new RetentionPolicyStub();
        var handler = new SubmitLetterHandler(
            journal, new AuthorizationGuard(evaluator), evaluator, policy, publisher);

        await handler.Handle(new SubmitLetterCommand(Actor, letter.Id), CancellationToken.None);

        journal.SubmissionCallback.Should().NotBeNull();
        await journal.SubmissionCallback!(CancellationToken.None);
        publisher.Published.Should().ContainSingle().Which
            .Should().BeOfType<LetterSubmittedDomainEvent>();
    }

    private sealed class RetentionPolicyStub : IRetentionPolicy
    {
        public RetentionAssignment Assign(string categoryCode, DateTime submittedOn) =>
            new("default", null);
    }

    /// <summary>Records the relative order of publications and saves.</summary>
    private sealed class RecordingPublisher(List<string> log) : IPublisher
    {
        public List<object> Published { get; } = [];

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification
        {
            Published.Add(notification!);
            log.Add($"publish:{notification!.GetType().Name}");
            return Task.CompletedTask;
        }

        public Task Publish(object notification, CancellationToken ct = default) =>
            throw new NotSupportedException("Object overloads are not part of the correspondence flows.");
    }

    private sealed class SequencedJournal(Letter letter, List<string> log) : ILetterJournal
    {
        public int SaveCount { get; private set; }
        public Func<CancellationToken, Task>? SubmissionCallback { get; private set; }

        public Task<Letter?> FindTrackedLetterAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Letter?>(letter.Id == id ? letter : null);

        public Task SaveAsync(Letter target, CancellationToken ct)
        {
            SaveCount++;
            log.Add("save");
            return Task.CompletedTask;
        }

        public Task FlushOutboxAsync(CancellationToken ct) => Task.CompletedTask;

        public Task SubmitAsync(
            Letter target, string retentionClass, DateTime? retentionExpiresOn,
            Func<CancellationToken, Task> publishSubmittedEvent, CancellationToken ct)
        {
            target.Submit(target.CreatedBy, 2026, 1, retentionClass, retentionExpiresOn, DateTime.UtcNow);
            SubmissionCallback = publishSubmittedEvent;
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

    private sealed class FailingSaveJournal(Letter letter, List<string> log) : ILetterJournal
    {
        public List<Letter> Saved { get; } = [];

        public Task<Letter?> FindTrackedLetterAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Letter?>(letter.Id == id ? letter : null);

        public Task SaveAsync(Letter target, CancellationToken ct)
        {
            log.Add("save-failed");
            throw new DbUpdateException("simulated persistence failure");
        }

        public Task FlushOutboxAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SubmitAsync(
            Letter target, string retentionClass, DateTime? retentionExpiresOn,
            Func<CancellationToken, Task> publishSubmittedEvent, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task SaveTemplateAsync(Template letterTemplate, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsActiveHoldAsync(Guid letterId, CancellationToken ct) => Task.FromResult(false);
        public Task SaveHoldsAsync(IReadOnlyList<LetterHold> holds, CancellationToken ct) => Task.CompletedTask;
        public Task SaveHoldReleaseAsync(LetterHold hold, CancellationToken ct) => Task.CompletedTask;
        public Task SaveExportActivityAsync(ExportActivity activity, CancellationToken ct) => Task.CompletedTask;
        public Task<PurgeBatchResult> PurgeExpiredBatchAsync(int maxBatchSize, DateTime asOf, CancellationToken ct) =>
            Task.FromResult(new PurgeBatchResult(0, 0));
    }
}

