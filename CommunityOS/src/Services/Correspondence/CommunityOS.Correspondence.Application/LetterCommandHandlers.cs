using CommunityOS.Correspondence.Application.Authorization;
using CommunityOS.Correspondence.Application.Permissions;
using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Domain.Events;
using CommunityOS.Correspondence.Domain.Exceptions;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using MediatR;

namespace CommunityOS.Correspondence.Application;

/// <summary>Uniform single-letter operation gate: the caller must hold the
/// required action permission at the letter's scope and be able to read the
/// letter (including the sensitive second pass); otherwise the letter is
/// indistinguishable from a missing one.</summary>
internal static class LetterOperationGate
{
    public static async Task<Domain.Letter> RequireAccessibleLetterAsync(
        ILetterJournal journal,
        IAuthorizationEvaluator evaluator,
        Guid actorId,
        string actionPermission,
        Guid letterId,
        CancellationToken ct)
    {
        var letter = await journal.FindTrackedLetterAsync(letterId, ct)
            ?? throw new LetterNotFoundException();

        if (!await LetterVisibility.CanActAsync(evaluator, actorId,
                LetterPermissions.LetterRead, letter.Id, letter.OrganizationUnitId, ct) ||
            (letter.Sensitivity == LetterSensitivity.Sensitive &&
             !await LetterVisibility.CanActAsync(evaluator, actorId,
                 LetterPermissions.LetterReadSensitive, letter.Id, letter.OrganizationUnitId, ct)) ||
            !await LetterVisibility.CanActAsync(evaluator, actorId,
                actionPermission, letter.Id, letter.OrganizationUnitId, ct))
        {
            throw new LetterNotFoundException();
        }

        return letter;
    }
}

public sealed record CreateLetterCommand(
    Guid ActorId,
    string Category,
    string Sensitivity,
    string? Subject,
    string? Body,
    Guid OrganizationUnitId,
    IReadOnlyList<RecipientInput> Recipients,
    Guid? TemplateId,
    Guid? RelatedLetterId) : IRequest<LetterDto>;

public sealed record RecipientInput(string Kind, Guid? PersonId, Guid? UnitId, string? DisplayLine);

/// <summary>Draft creation. Template-based creation snapshots the template's
/// skeleton for any omitted field; later template edits never mutate existing
/// letters (ADR-028 decision 6).</summary>
public sealed class CreateLetterHandler(
    ILetterJournal journal,
    ILetterReader reader,
    AuthorizationGuard guard)
    : IRequestHandler<CreateLetterCommand, LetterDto>
{
    public async Task<LetterDto> Handle(CreateLetterCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(
            command.ActorId, LetterPermissions.LetterCreate,
            new AuthorizationContext(command.OrganizationUnitId, LetterPermissions.ResourceType),
            cancellationToken);

        Guid? templateId = null;
        string? templateCode = null;
        var category = command.Category;
        var subject = command.Subject;
        var body = command.Body;

        if (command.TemplateId is { } tid)
        {
            var template = await reader.FindTemplateAsync(tid, cancellationToken)
                ?? throw new TemplateNotFoundException();
            if (!template.IsActive)
            {
                throw new TemplateNotFoundException();
            }

            templateId = template.Id;
            templateCode = template.Code;
            category = string.IsNullOrWhiteSpace(category) ? template.CategoryCode : category;
            subject = string.IsNullOrWhiteSpace(subject) ? template.SubjectTemplate : subject;
            body = string.IsNullOrWhiteSpace(body) ? template.BodyTemplate : body;
        }

        var now = DateTime.UtcNow;
        var sensitivity = ParseSensitivity(command.Sensitivity);
        var letter = Domain.Letter.CreateDraft(
            command.OrganizationUnitId, category, subject ?? string.Empty, body ?? string.Empty,
            sensitivity, command.ActorId, now,
            templateId, templateCode, command.RelatedLetterId);

        foreach (var input in command.Recipients)
        {
            var (kind, personId, unitId, displayLine) = ParseRecipient(input);
            letter.AddRecipient(kind, personId, unitId, displayLine, now);
        }

        await journal.SaveAsync(letter, cancellationToken);
        return ToDto(letter, isHeld: false);
    }

    internal static LetterSensitivity ParseSensitivity(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "sensitive" => LetterSensitivity.Sensitive,
            "normal" or null or "" => LetterSensitivity.Normal,
            _ => throw new ArgumentException("Sensitivity must be 'normal' or 'sensitive'.")
        };

    private static (RecipientKind Kind, Guid? PersonId, Guid? UnitId, string? DisplayLine) ParseRecipient(RecipientInput input)
    {
        var kind = input.Kind?.Trim().ToLowerInvariant() switch
        {
            "person" => RecipientKind.Person,
            "unit" => RecipientKind.Unit,
            "external" => RecipientKind.External,
            _ => throw new ArgumentException("Recipient kind must be 'person', 'unit' or 'external'.")
        };
        return (kind, input.PersonId, input.UnitId, input.DisplayLine);
    }

    internal static LetterDto ToDto(Domain.Letter letter, bool isHeld) =>
        new(letter.Id, letter.ReferenceNumber, letter.Subject, letter.Body, letter.CategoryCode,
            LetterFormatting.Sensitivity(letter.Sensitivity),
            LetterFormatting.Status(letter.Status), letter.Revision, letter.OrganizationUnitId,
            letter.Recipients
                .Select(r => new LetterRecipientDto(r.Id, LetterFormatting.Kind(r.Kind), r.PersonId, r.UnitId, r.DisplayLine))
                .ToList(),
            letter.DocumentLinks.OrderBy(l => l.MaterializedOn).FirstOrDefault()?.DocumentId,
            letter.RelatedLetterId, isHeld, letter.CreatedBy, letter.CreatedOn, letter.UpdatedOn,
            letter.SubmittedOn, letter.MaterializedOn, letter.DispatchedOn, letter.DeliveredOn, letter.CancelledOn,
            letter.RetentionClass, letter.RetentionExpiresOn);
}

public sealed record UpdateLetterContentCommand(
    Guid ActorId, Guid LetterId, string Subject, string Body, int ExpectedRevision) : IRequest<LetterDto>;

public sealed class UpdateLetterContentHandler(
    ILetterJournal journal,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<UpdateLetterContentCommand, LetterDto>
{
    public async Task<LetterDto> Handle(UpdateLetterContentCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LetterPermissions.LetterUpdate, ct: cancellationToken);

        var letter = await LetterOperationGate.RequireAccessibleLetterAsync(
            journal, evaluator, command.ActorId, LetterPermissions.LetterUpdate,
            command.LetterId, cancellationToken);

        if (command.ExpectedRevision != letter.Revision)
        {
            throw new LetterConflictException("The letter was modified by someone else; reload and retry.");
        }

        letter.UpdateContent(command.Subject, command.Body, DateTime.UtcNow);
        await journal.SaveAsync(letter, cancellationToken);
        return CreateLetterHandler.ToDto(letter, isHeld: false);
    }
}

public sealed record ConfirmLetterCommand(Guid ActorId, Guid LetterId) : IRequest<LetterDto>;

public sealed class ConfirmLetterHandler(
    ILetterJournal journal,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<ConfirmLetterCommand, LetterDto>
{
    public async Task<LetterDto> Handle(ConfirmLetterCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LetterPermissions.LetterUpdate, ct: cancellationToken);

        var letter = await LetterOperationGate.RequireAccessibleLetterAsync(
            journal, evaluator, command.ActorId, LetterPermissions.LetterUpdate,
            command.LetterId, cancellationToken);

        letter.Confirm(command.ActorId, DateTime.UtcNow);
        await journal.SaveAsync(letter, cancellationToken);
        return CreateLetterHandler.ToDto(letter, isHeld: false);
    }
}

public sealed record UnconfirmLetterCommand(Guid ActorId, Guid LetterId) : IRequest<LetterDto>;

public sealed class UnconfirmLetterHandler(
    ILetterJournal journal,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<UnconfirmLetterCommand, LetterDto>
{
    public async Task<LetterDto> Handle(UnconfirmLetterCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LetterPermissions.LetterUpdate, ct: cancellationToken);

        var letter = await LetterOperationGate.RequireAccessibleLetterAsync(
            journal, evaluator, command.ActorId, LetterPermissions.LetterUpdate,
            command.LetterId, cancellationToken);

        letter.Unconfirm(command.ActorId, DateTime.UtcNow);
        await journal.SaveAsync(letter, cancellationToken);
        return CreateLetterHandler.ToDto(letter, isHeld: false);
    }
}

public sealed record SubmitLetterResult(Guid LetterId, string Reference, int LetterYear, int LetterSequence);

public sealed record SubmitLetterCommand(Guid ActorId, Guid LetterId) : IRequest<SubmitLetterResult>;

/// <summary>
/// Submits a confirmed letter. The reference number allocation, state change,
/// history append and the outbox-captured <see cref="LetterSubmittedDomainEvent"/>
/// all commit in one transaction (ADR-028 decision 7). Dispatch remains blocked
/// until Documents materialization confirms.
/// </summary>
public sealed class SubmitLetterHandler(
    ILetterJournal journal,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator,
    IRetentionPolicy retentionPolicy,
    IPublisher publisher)
    : IRequestHandler<SubmitLetterCommand, SubmitLetterResult>
{
    public async Task<SubmitLetterResult> Handle(SubmitLetterCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LetterPermissions.LetterSubmit, ct: cancellationToken);

        var letter = await LetterOperationGate.RequireAccessibleLetterAsync(
            journal, evaluator, command.ActorId, LetterPermissions.LetterSubmit,
            command.LetterId, cancellationToken);

        var now = DateTime.UtcNow;
        var retention = retentionPolicy.Assign(letter.CategoryCode, now);

        // Invoked inside the open submission transaction after the number is
        // allocated; the journal flushes the buffered outbox rows atomically.
        Task PublishAsync(CancellationToken ct) =>
            publisher.Publish(LetterEventFactory.Submitted(letter), ct);

        await journal.SubmitAsync(letter, retention.Class, retention.ExpiresOn, PublishAsync, cancellationToken);
        return new(letter.Id, letter.ReferenceNumber!, letter.LetterYear!.Value, letter.LetterSequence!.Value);
    }
}

/// <summary>Builds ratified domain-event payloads from final aggregate state.
/// Payloads carry identifiers, codes, counts and timestamps only.</summary>
internal static class LetterEventFactory
{
    public static LetterSubmittedDomainEvent Submitted(Domain.Letter letter) =>
        new(letter.Id, letter.LetterYear!.Value, letter.LetterSequence!.Value,
            letter.OrganizationUnitId, letter.CategoryCode,
            LetterFormatting.Sensitivity(letter.Sensitivity),
            letter.Recipients.Count,
            letter.Recipients.Where(r => r.Kind == RecipientKind.Person).Select(r => r.PersonId!.Value).ToList(),
            letter.Recipients.Where(r => r.Kind == RecipientKind.Unit).Select(r => r.UnitId!.Value).ToList(),
            letter.SubmittedBy!.Value);

    public static (int Year, int Sequence) Number(Domain.Letter letter) =>
        (letter.LetterYear ?? 0, letter.LetterSequence ?? 0);
}

public sealed record CancelLetterResult(Guid LetterId, string Status);

public sealed record CancelLetterCommand(Guid ActorId, Guid LetterId, string ReasonCode) : IRequest<CancelLetterResult>;

/// <summary>Cancels pre-dispatch; the consumed reference number is retained
/// permanently. Unnumbered drafts cancel with a zero reference in the event.</summary>
public sealed class CancelLetterHandler(
    ILetterJournal journal,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator,
    IPublisher publisher)
    : IRequestHandler<CancelLetterCommand, CancelLetterResult>
{
    public async Task<CancelLetterResult> Handle(CancelLetterCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LetterPermissions.LetterCancel, ct: cancellationToken);

        var letter = await LetterOperationGate.RequireAccessibleLetterAsync(
            journal, evaluator, command.ActorId, LetterPermissions.LetterCancel,
            command.LetterId, cancellationToken);

        var fromStatus = LetterFormatting.Status(letter.Status);
        var (year, sequence) = LetterEventFactory.Number(letter);
        letter.Cancel(command.ActorId, command.ReasonCode.Trim(), DateTime.UtcNow);
        await journal.SaveAsync(letter, cancellationToken);

        await publisher.Publish(new LetterCancelledDomainEvent(
            letter.Id, year, sequence, letter.OrganizationUnitId, fromStatus,
            command.ReasonCode.Trim(), command.ActorId), cancellationToken);
        return new(letter.Id, LetterFormatting.Status(letter.Status));
    }
}

public sealed record DispatchLetterCommand(Guid ActorId, Guid LetterId, string MethodCode) : IRequest<LetterDto>;

/// <summary>Manual dispatch recording — the first gate has no external
/// providers; method code <c>manual</c> only.</summary>
public sealed class DispatchLetterHandler(
    ILetterJournal journal,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator,
    IPublisher publisher)
    : IRequestHandler<DispatchLetterCommand, LetterDto>
{
    public const string ManualMethodCode = "manual";

    public async Task<LetterDto> Handle(DispatchLetterCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LetterPermissions.LetterAdmin, ct: cancellationToken);

        var letter = await LetterOperationGate.RequireAccessibleLetterAsync(
            journal, evaluator, command.ActorId, LetterPermissions.LetterAdmin,
            command.LetterId, cancellationToken);

        var methodCode = command.MethodCode.Trim();
        letter.RecordDispatch(methodCode, command.ActorId, DateTime.UtcNow);
        await journal.SaveAsync(letter, cancellationToken);

        var (year, sequence) = LetterEventFactory.Number(letter);
        await publisher.Publish(new LetterDispatchedDomainEvent(
            letter.Id, year, sequence, letter.OrganizationUnitId, methodCode, command.ActorId),
            cancellationToken);
        return CreateLetterHandler.ToDto(letter, isHeld: false);
    }
}

public sealed record RecordDeliveryOutcomeCommand(
    Guid ActorId, Guid LetterId, string Outcome, string? ReasonCode) : IRequest<LetterDto>;

public sealed class RecordDeliveryOutcomeHandler(
    ILetterJournal journal,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator,
    IPublisher publisher)
    : IRequestHandler<RecordDeliveryOutcomeCommand, LetterDto>
{
    public async Task<LetterDto> Handle(RecordDeliveryOutcomeCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LetterPermissions.LetterAdmin, ct: cancellationToken);

        var letter = await LetterOperationGate.RequireAccessibleLetterAsync(
            journal, evaluator, command.ActorId, LetterPermissions.LetterAdmin,
            command.LetterId, cancellationToken);

        var methodCode = letter.LastDispatchMethodCode ?? DispatchLetterHandler.ManualMethodCode;
        var outcome = command.Outcome?.Trim().ToLowerInvariant() switch
        {
            "confirmed" => DeliveryOutcome.Confirmed,
            "failed" => DeliveryOutcome.Failed,
            _ => throw new ArgumentException("Outcome must be 'confirmed' or 'failed'.")
        };
        var now = DateTime.UtcNow;
        if (outcome == DeliveryOutcome.Confirmed)
        {
            letter.ConfirmDelivery(methodCode, command.ActorId, now);
        }
        else
        {
            letter.FailDelivery(methodCode, command.ReasonCode!.Trim(), command.ActorId, now);
        }

        await journal.SaveAsync(letter, cancellationToken);

        var (year, sequence) = LetterEventFactory.Number(letter);
        if (outcome == DeliveryOutcome.Confirmed)
        {
            await publisher.Publish(new LetterDeliveryConfirmedDomainEvent(
                letter.Id, year, sequence, letter.OrganizationUnitId, methodCode, command.ActorId),
                cancellationToken);
        }
        else
        {
            await publisher.Publish(new LetterDeliveryFailedDomainEvent(
                letter.Id, year, sequence, letter.OrganizationUnitId, methodCode,
                command.ReasonCode!.Trim(), command.ActorId), cancellationToken);
        }

        return CreateLetterHandler.ToDto(letter, isHeld: false);
    }
}
