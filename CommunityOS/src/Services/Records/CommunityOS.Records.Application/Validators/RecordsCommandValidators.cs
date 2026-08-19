using CommunityOS.Records.Application.Commands;
using FluentValidation;

namespace CommunityOS.Records.Application.Validators;

public sealed class CreateRecordCommandValidator : AbstractValidator<CreateRecordCommand>
{
    public CreateRecordCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SubjectType).NotEmpty().MaximumLength(30);
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.OrganizationUnitId).NotEmpty().When(x => x.OrganizationUnitId.HasValue);
        RuleFor(x => x.Fields).NotNull();
    }
}

public sealed class UpdateRecordFieldsCommandValidator : AbstractValidator<UpdateRecordFieldsCommand>
{
    public UpdateRecordFieldsCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.Fields).NotNull();
    }
}

public sealed class CorrectRecordCommandValidator : AbstractValidator<CorrectRecordCommand>
{
    public CorrectRecordCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.Fields).NotNull();
        RuleFor(x => x.ChangeReason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class SubmitRecordCommandValidator : AbstractValidator<SubmitRecordCommand>
{
    public SubmitRecordCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
    }
}

public sealed class MoveUnderReviewCommandValidator : AbstractValidator<MoveUnderReviewCommand>
{
    public MoveUnderReviewCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
    }
}

public sealed class VerifyRecordCommandValidator : AbstractValidator<VerifyRecordCommand>
{
    public VerifyRecordCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
    }
}

public sealed class RejectRecordCommandValidator : AbstractValidator<RejectRecordCommand>
{
    public RejectRecordCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
    }
}

public sealed class ClassifyRecordCommandValidator : AbstractValidator<ClassifyRecordCommand>
{
    public ClassifyRecordCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.ClassificationCode).MaximumLength(50);
        RuleFor(x => x.RetentionScheduleCode).MaximumLength(100);
    }
}

public sealed class ArchiveRecordCommandValidator : AbstractValidator<ArchiveRecordCommand>
{
    public ArchiveRecordCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
    }
}

public sealed class DeactivateRecordCommandValidator : AbstractValidator<DeactivateRecordCommand>
{
    public DeactivateRecordCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class RestoreRecordCommandValidator : AbstractValidator<RestoreRecordCommand>
{
    public RestoreRecordCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
    }
}

public sealed class AddRecordScopeCommandValidator : AbstractValidator<AddRecordScopeCommand>
{
    public AddRecordScopeCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
    }
}

public sealed class RemoveRecordScopeCommandValidator : AbstractValidator<RemoveRecordScopeCommand>
{
    public RemoveRecordScopeCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
    }
}

public sealed class AttachEvidenceCommandValidator : AbstractValidator<AttachEvidenceCommand>
{
    public AttachEvidenceCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.VersionNumber).GreaterThan(0);
        RuleFor(x => x.ReferenceType).NotEmpty().MaximumLength(50);
    }
}

public sealed class RemoveEvidenceCommandValidator : AbstractValidator<RemoveEvidenceCommand>
{
    public RemoveEvidenceCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.EvidenceId).NotEmpty();
    }
}

public sealed class PlaceHoldCommandValidator : AbstractValidator<PlaceHoldCommand>
{
    public PlaceHoldCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.HoldType).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class ReleaseHoldCommandValidator : AbstractValidator<ReleaseHoldCommand>
{
    public ReleaseHoldCommandValidator()
    {
        RuleFor(x => x.HoldId).NotEmpty();
    }
}

public sealed class FlagRecordRetentionExpiredCommandValidator
    : AbstractValidator<FlagRecordRetentionExpiredCommand>
{
    public FlagRecordRetentionExpiredCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
    }
}

public sealed class CreateRetentionScheduleCommandValidator
    : AbstractValidator<CreateRetentionScheduleCommand>
{
    public CreateRetentionScheduleCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Rules).NotNull().NotEmpty();
        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.RetentionPeriod).NotEmpty().MaximumLength(30);
            rule.RuleFor(r => r.StartTrigger).NotEmpty().MaximumLength(30);
            rule.RuleFor(r => r.Disposition).NotEmpty().MaximumLength(30);
            rule.RuleFor(r => r.Note).MaximumLength(500);
            rule.RuleFor(r => r.MaximumPeriod).MaximumLength(30);
        });
    }
}

public sealed class UpdateRetentionScheduleCommandValidator
    : AbstractValidator<UpdateRetentionScheduleCommand>
{
    public UpdateRetentionScheduleCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Rules).NotNull().NotEmpty();
        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.RetentionPeriod).NotEmpty().MaximumLength(30);
            rule.RuleFor(r => r.StartTrigger).NotEmpty().MaximumLength(30);
            rule.RuleFor(r => r.Disposition).NotEmpty().MaximumLength(30);
            rule.RuleFor(r => r.Note).MaximumLength(500);
            rule.RuleFor(r => r.MaximumPeriod).MaximumLength(30);
        });
    }
}

public sealed class RetireRetentionScheduleCommandValidator : AbstractValidator<RetireRetentionScheduleCommand>
{
    public RetireRetentionScheduleCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateRecordCategoryCommandValidator : AbstractValidator<CreateRecordCategoryCommand>
{
    public CreateRecordCategoryCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class UpdateRecordCategoryCommandValidator : AbstractValidator<UpdateRecordCategoryCommand>
{
    public UpdateRecordCategoryCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}