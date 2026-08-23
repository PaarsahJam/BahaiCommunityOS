using CommunityOS.Correspondence.Domain;
using FluentValidation;

namespace CommunityOS.Correspondence.Application.Validators;

public sealed class QueryLettersValidator : AbstractValidator<QueryLetters>
{
    public QueryLettersValidator()
    {
        RuleFor(x => x.Order)
            .Must(o => o is "asc" or "desc")
            .WithMessage("Order must be 'asc' or 'desc'.");
        RuleFor(x => x.Limit).GreaterThanOrEqualTo(0).When(x => x.Limit.HasValue);
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Filters.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) ||
                       Enum.TryParse<LetterStatus>(s.Trim(), ignoreCase: true, out _))
            .WithMessage("Unknown status filter.");
    }
}

public sealed class CreateLetterValidator : AbstractValidator<CreateLetterCommand>
{
    public CreateLetterValidator()
    {
        RuleFor(x => x.Category)
            .NotEmpty().When(x => !x.TemplateId.HasValue)
            .MaximumLength(50);
        RuleFor(x => x.Sensitivity)
            .Must(s => s is null || s.Trim().ToLowerInvariant() is "normal" or "sensitive")
            .WithMessage("Sensitivity must be 'normal' or 'sensitive'.");
        RuleFor(x => x.Subject).NotEmpty().When(x => !x.TemplateId.HasValue).MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().When(x => !x.TemplateId.HasValue);
        RuleFor(x => x.OrganizationUnitId).NotEqual(Guid.Empty);
        RuleFor(x => x.Recipients).NotNull().NotEmpty();
        RuleForEach(x => x.Recipients).ChildRules(recipient =>
        {
            recipient.RuleFor(r => r.Kind)
                .Must(k => k is "person" or "unit" or "external")
                .WithMessage("Recipient kind must be 'person', 'unit' or 'external'.");
            recipient.RuleFor(r => r.PersonId).NotNull().NotEqual(Guid.Empty)
                .When(r => r.Kind == "person");
            recipient.RuleFor(r => r.UnitId).NotNull().NotEqual(Guid.Empty)
                .When(r => r.Kind == "unit");
            recipient.RuleFor(r => r.DisplayLine).NotNull().NotEmpty().MaximumLength(500)
                .When(r => r.Kind == "external");
            recipient.RuleFor(r => r.DisplayLine).Null().When(r => r.Kind is "person" or "unit");
            recipient.RuleFor(r => r.PersonId).Null().When(r => r.Kind != "person");
            recipient.RuleFor(r => r.UnitId).Null().When(r => r.Kind != "unit");
        });
    }
}

public sealed class UpdateLetterContentValidator : AbstractValidator<UpdateLetterContentCommand>
{
    public UpdateLetterContentValidator()
    {
        RuleFor(x => x.LetterId).NotEqual(Guid.Empty);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.ExpectedRevision).GreaterThan(0);
    }
}

public sealed class CancelLetterValidator : AbstractValidator<CancelLetterCommand>
{
    public CancelLetterValidator()
    {
        RuleFor(x => x.ReasonCode)
            .Must(code => CorrespondenceReasonCodes.IsKnown(CorrespondenceReasonCodes.Cancellation, code))
            .WithMessage("ReasonCode must be one of: draft-error, superseded, withdrawn-by-institution, other.");
    }
}

public sealed class DispatchLetterValidator : AbstractValidator<DispatchLetterCommand>
{
    public DispatchLetterValidator()
    {
        // First gate: manual dispatch only; providers are a deferred seam.
        RuleFor(x => x.MethodCode)
            .Equal(DispatchLetterHandler.ManualMethodCode)
            .WithMessage($"MethodCode must be '{DispatchLetterHandler.ManualMethodCode}' at this gate.");
    }
}

public sealed record DeliveryOutcomeInput(string Outcome, string? ReasonCode);

public sealed class RecordDeliveryOutcomeValidator : AbstractValidator<RecordDeliveryOutcomeCommand>
{
    public RecordDeliveryOutcomeValidator()
    {
        RuleFor(x => x.Outcome)
            .Must(o => o is "confirmed" or "failed")
            .WithMessage("Outcome must be 'confirmed' or 'failed'.");
        RuleFor(x => x.ReasonCode)
            .Cascade(CascadeMode.Stop)
            .NotNull().NotEmpty()
            .Must(code => CorrespondenceReasonCodes.IsKnown(CorrespondenceReasonCodes.DeliveryFailure, code!))
            .When(x => x.Outcome == "failed")
            .WithMessage("ReasonCode must be one of: bad-address, refused, unclaimed, returned-to-sender, provider-error, other.");
        RuleFor(x => x.ReasonCode).Null().When(x => x.Outcome == "confirmed");
    }
}

public sealed class ExportLettersValidator : AbstractValidator<ExportLettersCommand>
{
    public ExportLettersValidator()
    {
        RuleFor(x => x.Format)
            .Must(f => f is "csv" or "ndjson")
            .WithMessage("Format must be 'csv' or 'ndjson'.");
        RuleFor(x => x.MaxRows)
            .GreaterThan(0)
            .When(x => x.MaxRows.HasValue);
    }
}

public sealed class PlaceHoldsValidator : AbstractValidator<PlaceHoldsCommand>
{
    public PlaceHoldsValidator()
    {
        RuleFor(x => x.LetterIds).NotNull().NotEmpty();
        RuleFor(x => x.HoldType)
            .Must(t => t is LetterHold.HoldTypeLegal or LetterHold.HoldTypeAdministrative)
            .WithMessage("HoldType must be 'legal' or 'administrative'.");
        RuleFor(x => x.ReasonCode)
            .Must(code => CorrespondenceReasonCodes.IsKnown(CorrespondenceReasonCodes.HoldPlacement, code))
            .WithMessage("ReasonCode must be one of: investigation, legal-request, dispute, regulatory-inquiry, other.");
    }
}

public sealed class PurgeExpiredValidator : AbstractValidator<PurgeExpiredLettersCommand>
{
    public PurgeExpiredValidator()
    {
        // Negative batch sizes are rejected (400); zero means configured
        // default; values above the maximum are clamped by the handler.
        RuleFor(x => x.MaxBatchSize).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateTemplateValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SubjectTemplate).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BodyTemplate).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
    }
}
