using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Records.Application.Abstractions;
using CommunityOS.Records.Application.Authorization;
using CommunityOS.Records.Application.DTOs;
using CommunityOS.Records.Application.Logging;
using CommunityOS.Records.Application.Permissions;
using CommunityOS.Records.Application.Pipeline;
using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Enumerations;
using CommunityOS.Records.Domain.Exceptions;
using CommunityOS.Records.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using static CommunityOS.Records.Application.Commands.RecordCommandHelpers;
using RecordsEventsPublisher = CommunityOS.Records.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Records.Application.Commands;

public sealed record CreateRecordCommand(
    Guid ActorId,
    string Category,
    string SubjectType,
    Guid SubjectId,
    Guid? OrganizationUnitId,
    IReadOnlyList<RecordFieldValue> Fields,
    bool IsSensitive) : IRequest<RecordDto>;

internal sealed class CreateRecordCommandHandler(
    IRecordRepository records,
    IRecordCategoryRepository categories,
    IOrganizationUnitReferenceRepository units,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<CreateRecordCommandHandler> logger)
    : IRequestHandler<CreateRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(CreateRecordCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, RecordsPermissions.RecordCreate,
            new AuthorizationContext(ResourceType: "record"), ct);

        await EnsureCategoryExistsAsync(categories, cmd.Category, ct);
        if (!RecordSubjectTypes.IsValid(cmd.SubjectType))
            throw new InvalidRecordSubjectTypeException(cmd.SubjectType);
        await EnsureUnitExistsAsync(units, cmd.OrganizationUnitId, ct);

        var record = Record.Create(
            cmd.Category,
            cmd.SubjectType,
            cmd.SubjectId,
            cmd.OrganizationUnitId,
            cmd.Fields,
            cmd.IsSensitive,
            cmd.ActorId,
            DateTime.UtcNow);

        // Outbox: publish the created domain event before the single
        // transaction commits so the forwarded integration event and the
        // record row are committed atomically (ADR-015, ratified).
        await RecordsEventsPublisher.PublishAsync(record, mediator, ct);
        await records.AddAsync(record, ct);

        logger.RecordCreated(record.Id, record.Category);
        return record.ToDto();
    }
}

public sealed record UpdateRecordFieldsCommand(
    Guid ActorId,
    Guid RecordId,
    IReadOnlyList<RecordFieldValue> Fields) : IRequest<RecordDto>;

internal sealed class UpdateRecordFieldsCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<UpdateRecordFieldsCommand, RecordDto>
{
    public async Task<RecordDto> Handle(UpdateRecordFieldsCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordUpdate, record, ct);

        record.UpdateFields(cmd.Fields, cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record SubmitRecordCommand(Guid ActorId, Guid RecordId) : IRequest<RecordDto>;

internal sealed class SubmitRecordCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<SubmitRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(SubmitRecordCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordSubmit, record, ct);

        record.Submit(cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record MoveUnderReviewCommand(Guid ActorId, Guid RecordId) : IRequest<RecordDto>;

internal sealed class MoveUnderReviewCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<MoveUnderReviewCommand, RecordDto>
{
    public async Task<RecordDto> Handle(MoveUnderReviewCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordVerify, record, ct);

        record.MoveUnderReview(cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record VerifyRecordCommand(Guid ActorId, Guid RecordId) : IRequest<RecordDto>;

internal sealed class VerifyRecordCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<VerifyRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(VerifyRecordCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordVerify, record, ct);

        record.Verify(cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record RejectRecordCommand(Guid ActorId, Guid RecordId) : IRequest<RecordDto>;

internal sealed class RejectRecordCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<RejectRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(RejectRecordCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordVerify, record, ct);

        record.Reject(cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record CorrectRecordCommand(
    Guid ActorId,
    Guid RecordId,
    IReadOnlyList<RecordFieldValue> Fields,
    string ChangeReason) : IRequest<RecordDto>;

internal sealed class CorrectRecordCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<CorrectRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(CorrectRecordCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordCorrect, record, ct);

        record.Correct(cmd.Fields, cmd.ChangeReason, cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record ClassifyRecordCommand(
    Guid ActorId,
    Guid RecordId,
    string? ClassificationCode,
    bool IsSensitive,
    string? RetentionScheduleCode) : IRequest<RecordDto>;

internal sealed class ClassifyRecordCommandHandler(
    IRecordRepository records,
    IRetentionScheduleRepository retention,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<ClassifyRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(ClassifyRecordCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordClassify, record, ct);

        if (!string.IsNullOrWhiteSpace(cmd.RetentionScheduleCode) &&
            !await retention.ExistsByCodeAsync(cmd.RetentionScheduleCode, ct))
            throw new RetentionScheduleNotFoundException(cmd.RetentionScheduleCode);

        record.SetClassification(
            cmd.ClassificationCode,
            cmd.IsSensitive,
            cmd.RetentionScheduleCode,
            cmd.ActorId,
            DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record ArchiveRecordCommand(Guid ActorId, Guid RecordId) : IRequest<RecordDto>;

internal sealed class ArchiveRecordCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<ArchiveRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(ArchiveRecordCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordArchive, record, ct);

        record.Archive(cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record DeactivateRecordCommand(
    Guid ActorId,
    Guid RecordId,
    bool AdminOverride,
    string? Reason) : IRequest<RecordDto>;

internal sealed class DeactivateRecordCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<DeactivateRecordCommandHandler> logger)
    : IRequestHandler<DeactivateRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(DeactivateRecordCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);

        await RecordAuthorization.RequireForRecordAsync(
            guard, cmd.ActorId, RecordsPermissions.RecordDeactivate, record, ct);
        if (cmd.AdminOverride)
        {
            await RecordAuthorization.RequireForRecordAsync(
                guard, cmd.ActorId, RecordsPermissions.RecordAdmin, record, ct);
            logger.AdminOverrideApplied(record.Id, "deactivate");
        }

        record.Deactivate(cmd.ActorId, cmd.AdminOverride, cmd.Reason, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record RestoreRecordCommand(Guid ActorId, Guid RecordId) : IRequest<RecordDto>;

internal sealed class RestoreRecordCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<RestoreRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(RestoreRecordCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordRestore, record, ct);

        record.Restore(cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record AddRecordScopeCommand(
    Guid ActorId,
    Guid RecordId,
    Guid OrganizationUnitId) : IRequest<RecordDto>;

internal sealed class AddRecordScopeCommandHandler(
    IRecordRepository records,
    IOrganizationUnitReferenceRepository units,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<AddRecordScopeCommand, RecordDto>
{
    public async Task<RecordDto> Handle(AddRecordScopeCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordScopeManage, record, ct);

        await EnsureUnitExistsAsync(units, cmd.OrganizationUnitId, ct);

        record.AddOrganizationScope(cmd.OrganizationUnitId, cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record RemoveRecordScopeCommand(
    Guid ActorId,
    Guid RecordId,
    Guid OrganizationUnitId) : IRequest<RecordDto>;

internal sealed class RemoveRecordScopeCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<RemoveRecordScopeCommand, RecordDto>
{
    public async Task<RecordDto> Handle(RemoveRecordScopeCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordScopeManage, record, ct);

        record.RemoveOrganizationScope(cmd.OrganizationUnitId, cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record AttachEvidenceCommand(
    Guid ActorId,
    Guid RecordId,
    Guid DocumentId,
    int VersionNumber,
    string ReferenceType) : IRequest<RecordEvidenceReferenceDto>;

internal sealed class AttachEvidenceCommandHandler(
    IRecordRepository records,
    IDocumentsServiceClient documents,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<AttachEvidenceCommandHandler> logger)
    : IRequestHandler<AttachEvidenceCommand, RecordEvidenceReferenceDto>
{
    public async Task<RecordEvidenceReferenceDto> Handle(AttachEvidenceCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordEvidenceManage, record, ct);

        var evidence = record.AttachEvidence(
            cmd.DocumentId, cmd.VersionNumber, cmd.ReferenceType, cmd.ActorId, DateTime.UtcNow);

        try
        {
            // Mirror the reference onto the document (SourceContext = records.record).
            await documents.CreateReferenceAsync(
                cmd.DocumentId, "records.record", record.Id, evidence.ReferenceType, ct);
        }
        catch (Exception ex)
        {
            logger.DocumentsCommandFailed(record.Id, cmd.DocumentId, "attach-evidence", ex);
            throw;
        }

        await SaveAsync(record, records, mediator, ct);
        return evidence.ToDto();
    }
}

public sealed record RemoveEvidenceCommand(
    Guid ActorId,
    Guid RecordId,
    Guid EvidenceId) : IRequest<RecordDto>;

internal sealed class RemoveEvidenceCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<RemoveEvidenceCommand, RecordDto>
{
    public async Task<RecordDto> Handle(RemoveEvidenceCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordEvidenceManage, record, ct);

        record.RemoveEvidence(cmd.EvidenceId, cmd.ActorId, DateTime.UtcNow);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record PlaceHoldCommand(
    Guid ActorId,
    Guid RecordId,
    string HoldType,
    string Reason,
    IReadOnlyList<RecordHoldDocumentReference>? DocumentReferences) : IRequest<RecordHoldDto>;

internal sealed class PlaceHoldCommandHandler(
    IRecordRepository records,
    IDocumentsServiceClient documents,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<PlaceHoldCommandHandler> logger)
    : IRequestHandler<PlaceHoldCommand, RecordHoldDto>
{
    public async Task<RecordHoldDto> Handle(PlaceHoldCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.HoldManage, record, ct);

        var hold = record.PlaceHold(
            cmd.HoldType, cmd.Reason, cmd.DocumentReferences, cmd.ActorId, DateTime.UtcNow);

        if (cmd.DocumentReferences is not null)
        {
            foreach (var reference in cmd.DocumentReferences)
                await ApplyDocumentHoldAsync(documents, logger, record.Id, hold, reference, place: true, ct);
        }

        await SaveAsync(record, records, mediator, ct);
        return hold.ToDto(record.Id);
    }
}

public sealed record ReleaseHoldCommand(Guid ActorId, Guid HoldId) : IRequest<RecordHoldDto>;

internal sealed class ReleaseHoldCommandHandler(
    IRecordRepository records,
    IDocumentsServiceClient documents,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<ReleaseHoldCommandHandler> logger)
    : IRequestHandler<ReleaseHoldCommand, RecordHoldDto>
{
    public async Task<RecordHoldDto> Handle(ReleaseHoldCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.HoldId, ct, byHoldId: true);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.HoldManage, record, ct);

        var hold = record.Holds.FirstOrDefault(h => h.Id == cmd.HoldId)
            ?? throw new RecordHoldNotFoundException(cmd.HoldId);

        record.ReleaseHold(cmd.HoldId, cmd.ActorId, DateTime.UtcNow);

        if (hold.DocumentReferences.Count != 0)
        {
            foreach (var reference in hold.DocumentReferences)
                await ApplyDocumentHoldAsync(documents, logger, record.Id, hold, reference, place: false, ct);
        }

        await SaveAsync(record, records, mediator, ct);
        return hold.ToDto(record.Id);
    }
}

public sealed record FlagRecordRetentionExpiredCommand(
    Guid ActorId,
    Guid RecordId,
    DateTime ExpiredOn) : IRequest<RecordDto>;

internal sealed class FlagRecordRetentionExpiredCommandHandler(
    IRecordRepository records,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<FlagRecordRetentionExpiredCommand, RecordDto>
{
    public async Task<RecordDto> Handle(FlagRecordRetentionExpiredCommand cmd, CancellationToken ct)
    {
        var record = await LoadForMutationAsync(records, cmd.RecordId, ct);
        await RecordAuthorization.RequireForRecordAsync(guard, cmd.ActorId, RecordsPermissions.RecordClassify, record, ct);

        record.FlagRetentionExpired(cmd.ExpiredOn);
        await SaveAsync(record, records, mediator, ct);

        return record.ToDto();
    }
}

public sealed record CreateRetentionScheduleCommand(
    Guid ActorId,
    string Code,
    string DisplayName,
    string? Description,
    IReadOnlyList<string> CategoryCodes,
    IReadOnlyList<RetentionRuleInput> Rules) : IRequest<RetentionScheduleDto>;

internal sealed class CreateRetentionScheduleCommandHandler(
    IRetentionScheduleRepository retention,
    AuthorizationGuard guard)
    : IRequestHandler<CreateRetentionScheduleCommand, RetentionScheduleDto>
{
    public async Task<RetentionScheduleDto> Handle(CreateRetentionScheduleCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, RecordsPermissions.RetentionManage,
            new AuthorizationContext(ResourceType: "record"), ct);

        if (await retention.ExistsByCodeAsync(cmd.Code, ct))
            throw new DuplicateRetentionScheduleException(cmd.Code);

        var schedule = RetentionSchedule.Create(
            cmd.Code,
            cmd.DisplayName,
            cmd.Description,
            BuildRules(cmd.CategoryCodes, cmd.Rules),
            cmd.ActorId);

        await retention.AddAsync(schedule, ct);
        return schedule.ToDto();
    }

    private static List<RetentionRule> BuildRules(
        IReadOnlyList<string> categoryCodes, IReadOnlyList<RetentionRuleInput> inputs)
    {
        var categories = categoryCodes.Count == 0 ? new[] { "*" } : categoryCodes;
        var rules = new List<RetentionRule>();
        foreach (var category in categories)
        {
            foreach (var input in inputs)
            {
                rules.Add(RetentionRule.Create(
                    category, input.StartTrigger, input.RetentionPeriod, input.Disposition, input.Note, input.MaximumPeriod));
            }
        }

        return rules;
    }
}

public sealed record UpdateRetentionScheduleCommand(
    Guid ActorId,
    string Code,
    string DisplayName,
    string? Description,
    IReadOnlyList<string> CategoryCodes,
    IReadOnlyList<RetentionRuleInput> Rules) : IRequest<RetentionScheduleDto>;

internal sealed class UpdateRetentionScheduleCommandHandler(
    IRetentionScheduleRepository retention,
    AuthorizationGuard guard)
    : IRequestHandler<UpdateRetentionScheduleCommand, RetentionScheduleDto>
{
    public async Task<RetentionScheduleDto> Handle(UpdateRetentionScheduleCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, RecordsPermissions.RetentionManage,
            new AuthorizationContext(ResourceType: "record"), ct);

        var schedule = await retention.GetByCodeAsync(cmd.Code, ct)
            ?? throw new RetentionScheduleNotFoundException(cmd.Code);

        schedule.Update(
            cmd.DisplayName,
            cmd.Description,
            BuildRules(cmd.CategoryCodes, cmd.Rules),
            cmd.ActorId);

        await retention.UpdateAsync(schedule, ct);
        return schedule.ToDto();
    }

    private static List<RetentionRule> BuildRules(
        IReadOnlyList<string> categoryCodes, IReadOnlyList<RetentionRuleInput> inputs)
    {
        var categories = categoryCodes.Count == 0 ? new[] { "*" } : categoryCodes;
        var rules = new List<RetentionRule>();
        foreach (var category in categories)
        {
            foreach (var input in inputs)
            {
                rules.Add(RetentionRule.Create(
                    category, input.StartTrigger, input.RetentionPeriod, input.Disposition, input.Note, input.MaximumPeriod));
            }
        }

        return rules;
    }
}

public sealed record RetireRetentionScheduleCommand(Guid ActorId, string Code)
    : IRequest<RetentionScheduleDto>;

internal sealed class RetireRetentionScheduleCommandHandler(
    IRetentionScheduleRepository retention,
    IRecordRepository records,
    AuthorizationGuard guard)
    : IRequestHandler<RetireRetentionScheduleCommand, RetentionScheduleDto>
{
    public async Task<RetentionScheduleDto> Handle(RetireRetentionScheduleCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, RecordsPermissions.RetentionManage,
            new AuthorizationContext(ResourceType: "record"), ct);

        var schedule = await retention.GetByCodeAsync(cmd.Code, ct)
            ?? throw new RetentionScheduleNotFoundException(cmd.Code);

        if (await records.ExistsByRetentionScheduleCodeAsync(cmd.Code, ct))
            throw new RetentionScheduleInUseException(cmd.Code);

        schedule.Retire(cmd.ActorId);
        await retention.UpdateAsync(schedule, ct);
        return schedule.ToDto();
    }
}

public sealed record CreateRecordCategoryCommand(
    Guid ActorId,
    string Code,
    string DisplayName,
    string? Description) : IRequest<RecordCategoryDto>;

internal sealed class CreateRecordCategoryCommandHandler(
    IRecordCategoryRepository categories,
    AuthorizationGuard guard)
    : IRequestHandler<CreateRecordCategoryCommand, RecordCategoryDto>
{
    public async Task<RecordCategoryDto> Handle(CreateRecordCategoryCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, RecordsPermissions.CategoryManage,
            new AuthorizationContext(ResourceType: "record"), ct);

        if (await categories.ExistsByCodeAsync(cmd.Code, ct))
            throw new DuplicateRecordCategoryException(cmd.Code);

        var category = RecordCategory.Create(cmd.Code, cmd.DisplayName, cmd.Description, cmd.ActorId);
        await categories.AddAsync(category, ct);
        return category.ToDto();
    }
}

public sealed record UpdateRecordCategoryCommand(
    Guid ActorId,
    string Code,
    string DisplayName,
    string? Description) : IRequest<RecordCategoryDto>;

internal sealed class UpdateRecordCategoryCommandHandler(
    IRecordCategoryRepository categories,
    AuthorizationGuard guard)
    : IRequestHandler<UpdateRecordCategoryCommand, RecordCategoryDto>
{
    public async Task<RecordCategoryDto> Handle(UpdateRecordCategoryCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, RecordsPermissions.CategoryManage,
            new AuthorizationContext(ResourceType: "record"), ct);

        var category = await categories.GetByCodeAsync(cmd.Code, ct)
            ?? throw new RecordCategoryNotFoundException(cmd.Code);

        category.Update(cmd.DisplayName, cmd.Description, cmd.ActorId);
        await categories.UpdateAsync(category, ct);
        return category.ToDto();
    }
}

/// <summary>Input model for a retention rule within a schedule request.</summary>
public sealed record RetentionRuleInput(
    string RetentionPeriod,
    string StartTrigger,
    string Disposition,
    string? Note,
    string? MaximumPeriod = null);

internal static class RecordCommandHelpers
{
    /// <summary>
    /// Loads the record for a guarded mutation. Ordering guarantees that
    /// authorization is evaluated before any persistence side effect: the
    /// resource is loaded, then guarded, then mutated and saved.
    /// </summary>
    public static async Task<Record> LoadForMutationAsync(
        IRecordRepository records,
        Guid id,
        CancellationToken ct,
        bool byHoldId = false) =>
        byHoldId
            ? await records.GetByHoldIdAsync(id, ct) ?? throw new RecordNotFoundException(id)
            : await records.GetByIdAsync(id, ct) ?? throw new RecordNotFoundException(id);

    /// <summary>
    /// Saves the aggregate. The domain events are published BEFORE SaveChanges
    /// so the forwarded integration events are written into the outbox and
    /// committed atomically with the record change (ADR-015, ratified — the
    /// Records outbox gate).
    /// </summary>
    public static async Task SaveAsync(
        Record record,
        IRecordRepository records,
        IMediator mediator,
        CancellationToken ct)
    {
        await RecordsEventsPublisher.PublishAsync(record, mediator, ct);
        await records.UpdateAsync(record, ct);
    }

    internal static async Task EnsureCategoryExistsAsync(
        IRecordCategoryRepository categories, string categoryCode, CancellationToken ct)
    {
        if (!await categories.ExistsByCodeAsync(categoryCode, ct))
            throw new InvalidRecordCategoryReferenceException(categoryCode);
    }

    internal static async Task EnsureUnitExistsAsync(
        IOrganizationUnitReferenceRepository units, Guid? unitId, CancellationToken ct)
    {
        if (unitId is null)
            return;

        if (!await units.ExistsAsync(unitId.Value, ct))
            throw new InvalidRecordScopeException(unitId.Value);
    }

    /// <summary>
    /// Applies or clears a document-level hold reference through the Documents
    /// classify surface. Classify is a full-replacement operation, so the
    /// current classification/sensitive/retention values are read and resubmitted
    /// alongside the changed hold reference (runbook note).
    /// </summary>
    internal static async Task ApplyDocumentHoldAsync(
        IDocumentsServiceClient documents,
        ILogger logger,
        Guid recordId,
        RecordHold hold,
        RecordHoldDocumentReference reference,
        bool place,
        CancellationToken ct)
    {
        try
        {
            var current = await documents.GetClassificationAsync(reference.DocumentId, ct);

            var legalRef = current.LegalHoldReference;
            var adminRef = current.AdministrativeHoldReference;
            if (place)
            {
                if (hold.HoldType == RecordHoldTypes.Legal)
                    legalRef = hold.Id.ToString();
                else if (hold.HoldType == RecordHoldTypes.Administrative)
                    adminRef = hold.Id.ToString();
            }
            else
            {
                if (hold.HoldType == RecordHoldTypes.Legal &&
                    string.Equals(legalRef, hold.Id.ToString(), StringComparison.Ordinal))
                    legalRef = null;
                if (hold.HoldType == RecordHoldTypes.Administrative &&
                    string.Equals(adminRef, hold.Id.ToString(), StringComparison.Ordinal))
                    adminRef = null;
            }

            await documents.ClassifyAsync(reference.DocumentId,
                current with { LegalHoldReference = legalRef, AdministrativeHoldReference = adminRef }, ct);
        }
        catch (Exception ex)
        {
            logger.DocumentsCommandFailed(recordId, reference.DocumentId,
                place ? "place-hold" : "release-hold", ex);
            throw;
        }
    }
}