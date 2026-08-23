using CommunityOS.Correspondence.Application.Authorization;
using CommunityOS.Correspondence.Application.Permissions;
using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Domain.Exceptions;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace CommunityOS.Correspondence.Application;

public sealed record ReconcileMaterializationsResult(int Inspected, int Republished);

public sealed record ReconcileMaterializationsCommand(Guid ActorId) : IRequest<ReconcileMaterializationsResult>;

/// <summary>
/// Operator reconciliation for stranded submissions (ADR-028 decision 7):
/// letters stuck in Submitted beyond the configured materialization window
/// have their original LetterSubmitted facts re-published verbatim.
/// Publication is safe — Documents-side materialization is idempotent per
/// source context+entity. The Documents consumer itself is gated on the
/// producer outbox upgrade and deliberately not registered at this gate;
/// re-publications are captured in the Correspondence outbox and converge
/// once the gate opens.
/// </summary>
public sealed class ReconcileMaterializationsHandler(
    ILetterReader reader,
    ILetterJournal journal,
    AuthorizationGuard guard,
    IPublisher publisher,
    IOptions<CorrespondenceOptions> options)
    : IRequestHandler<ReconcileMaterializationsCommand, ReconcileMaterializationsResult>
{
    internal const int MaxInspection = 1000;

    public async Task<ReconcileMaterializationsResult> Handle(
        ReconcileMaterializationsCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LetterPermissions.LetterAdmin, ct: cancellationToken);

        var olderThan = DateTime.UtcNow.AddMinutes(-options.Value.MaterializationWarnMinutes);
        var stuck = await reader.ListStuckSubmissionsAsync(olderThan, MaxInspection, cancellationToken);

        var republished = 0;
        foreach (var row in stuck)
        {
            var letter = await journal.FindTrackedLetterAsync(row.Id, cancellationToken);
            if (letter is null || letter.Status != Domain.LetterStatus.Submitted)
            {
                continue;
            }

            await publisher.Publish(LetterEventFactory.Submitted(letter), cancellationToken);
            republished++;
        }

        // Persist the buffered outbox rows (idempotent re-publications).
        await journal.FlushOutboxAsync(cancellationToken);

        return new(stuck.Count, republished);
    }
}

public sealed record PlaceHoldsCommand(
    Guid ActorId, IReadOnlyList<Guid> LetterIds, string HoldType, string ReasonCode)
    : IRequest<IReadOnlyList<HoldRow>>;

/// <summary>
/// Places legal/administrative holds (ADR-028 decision 13). Whole-batch
/// atomicity: every referenced letter must exist and be readable by the caller
/// (including the sensitive second pass); a target already carrying an active
/// hold conflicts. Reason codes come from the fixed vocabulary.
/// </summary>
public sealed class PlaceHoldsHandler(
    ILetterReader reader,
    ILetterJournal journal,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator,
    IOptions<CorrespondenceOptions> options)
    : IRequestHandler<PlaceHoldsCommand, IReadOnlyList<HoldRow>>
{
    public async Task<IReadOnlyList<HoldRow>> Handle(PlaceHoldsCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LetterPermissions.LetterAdmin, ct: cancellationToken);

        var ids = command.LetterIds.Distinct().ToList();
        if (ids.Count > options.Value.HoldMaxBatchSize)
        {
            throw new ArgumentException(
                $"A hold placement addresses at most {options.Value.HoldMaxBatchSize} letters.");
        }

        var targets = await reader.FindRangeSummariesAsync(ids, cancellationToken);

        // An unknown or invisible target fails the entire request without
        // disclosing which id was problematic.
        if (targets.Count != ids.Count ||
            !await AllVisibleAsync(command.ActorId, targets, cancellationToken))
        {
            throw new LetterNotFoundException();
        }

        foreach (var target in targets)
        {
            if (await reader.HasActiveHoldAsync(target.Id, cancellationToken))
            {
                throw new LetterConflictException("One or more letters already carry an active hold.");
            }
        }

        var now = DateTime.UtcNow;
        var holds = targets
            .Select(t => LetterHold.Create(t.Id, command.HoldType, command.ReasonCode, command.ActorId, now))
            .ToList();
        await journal.SaveHoldsAsync(holds, cancellationToken);

        return holds
            .Select(h => new HoldRow(h.Id, h.LetterId, h.HoldType, h.ReasonCode,
                h.PlacedBy, h.PlacedOn, h.ReleasedBy, h.ReleasedOn))
            .ToList();
    }

    private async Task<bool> AllVisibleAsync(Guid actorId, IReadOnlyList<LetterSummaryRow> rows, CancellationToken ct)
    {
        foreach (var row in rows)
        {
            if (!await LetterVisibility.IsVisibleAsync(evaluator, actorId, row, includeSensitive: true, ct))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record ReleaseHoldCommand(Guid ActorId, Guid HoldId) : IRequest<HoldRow>;

public sealed class ReleaseHoldHandler(
    ILetterReader reader,
    ILetterJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<ReleaseHoldCommand, HoldRow>
{
    public async Task<HoldRow> Handle(ReleaseHoldCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LetterPermissions.LetterAdmin, ct: cancellationToken);

        var hold = await reader.FindHoldAsync(command.HoldId, cancellationToken)
            ?? throw new LetterNotFoundException();

        hold.Release(command.ActorId, DateTime.UtcNow);
        await journal.SaveHoldReleaseAsync(hold, cancellationToken);

        return new(hold.Id, hold.LetterId, hold.HoldType, hold.ReasonCode,
            hold.PlacedBy, hold.PlacedOn, hold.ReleasedBy, hold.ReleasedOn);
    }
}

public sealed record PurgeExpiredResult(int PurgedCount, int RemainingExpiredEstimate);

public sealed record PurgeExpiredLettersCommand(Guid ActorId, int MaxBatchSize) : IRequest<PurgeExpiredResult>;

/// <summary>
/// Executes one retention-purge batch (ADR-028 decision 13). Expiry alone
/// never deletes: this administrative step selects up to the bounded batch of
/// expired unheld letters, appends purge tombstones inside the same guarded
/// transaction, then removes exactly that batch. Active holds always override
/// expiry.
/// </summary>
public sealed class PurgeExpiredLettersHandler(
    ILetterJournal journal,
    AuthorizationGuard guard,
    IOptions<CorrespondenceOptions> options)
    : IRequestHandler<PurgeExpiredLettersCommand, PurgeExpiredResult>
{
    public async Task<PurgeExpiredResult> Handle(PurgeExpiredLettersCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LetterPermissions.LetterAdmin, ct: cancellationToken);

        var size = Math.Clamp(
            command.MaxBatchSize == 0 ? options.Value.PurgeDefaultBatchSize : command.MaxBatchSize,
            1, options.Value.PurgeMaxBatchSize);

        var result = await journal.PurgeExpiredBatchAsync(size, DateTime.UtcNow, cancellationToken);
        return new(result.PurgedCount, result.RemainingExpired);
    }
}
