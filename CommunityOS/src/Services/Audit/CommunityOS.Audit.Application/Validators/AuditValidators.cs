using CommunityOS.Audit.Domain;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CommunityOS.Audit.Application.Validators;

public sealed class AuditQueryValidator : AbstractValidator<AuditQuery>
{
    public AuditQueryValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();

        RuleFor(x => x.Order)
            .Must(order => order is "asc" or "desc")
            .WithMessage("order must be 'asc' or 'desc'.");

        RuleFor(x => x.Limit)
            .GreaterThanOrEqualTo(1)
            .When(x => x.Limit.HasValue)
            .WithMessage("limit must be greater than or equal to 1.");

        RuleFor(x => x.Offset)
            .GreaterThanOrEqualTo(0)
            .WithMessage("offset must be greater than or equal to 0.");

        RuleFor(x => x.Filters.SourceService)
            .MaximumLength(50)
            .When(x => x.Filters.SourceService is not null);

        RuleFor(x => x.Filters.EventType)
            .MaximumLength(100)
            .When(x => x.Filters.EventType is not null);

        RuleFor(x => x.Filters.Action)
            .MaximumLength(100)
            .When(x => x.Filters.Action is not null);

        RuleFor(x => x.Filters.ResourceType)
            .MaximumLength(50)
            .When(x => x.Filters.ResourceType is not null);

        RuleFor(x => x.Filters)
            .Must(f => f.OccurredFrom is null || f.OccurredTo is null || f.OccurredFrom <= f.OccurredTo)
            .WithMessage("occurredFrom must not be after occurredTo.");
    }
}

public sealed class GetAuditEntryValidator : AbstractValidator<GetAuditEntry>
{
    public GetAuditEntryValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.EntryId).NotEmpty();
    }
}

public sealed class ExportAuditCommandValidator : AbstractValidator<ExportAuditCommand>
{
    public ExportAuditCommandValidator(IOptions<AuditOptions> options)
    {
        RuleFor(x => x.ActorId).NotEmpty();

        RuleFor(x => x.Format)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(format =>
                format.Trim().Equals("csv", StringComparison.OrdinalIgnoreCase) ||
                format.Trim().Equals("ndjson", StringComparison.OrdinalIgnoreCase))
            .WithMessage("format must be 'csv' or 'ndjson'.");

        RuleFor(x => x.MaxRows)
            .InclusiveBetween(1, options.Value.ExportMaxRows)
            .When(x => x.MaxRows.HasValue)
            .WithMessage($"maxRows must be between 1 and {options.Value.ExportMaxRows}.");
    }
}

public sealed class PlaceHoldCommandValidator : AbstractValidator<PlaceHoldCommand>
{
    public PlaceHoldCommandValidator(IOptions<AuditOptions> options)
    {
        RuleFor(x => x.ActorId).NotEmpty();

        RuleFor(x => x.EntryIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("entryIds must be non-empty identifiers.")
            .Must(ids => ids.Count <= options.Value.HoldMaxBatchSize)
            .WithMessage($"At most {options.Value.HoldMaxBatchSize} entries can be held per request.");

        RuleFor(x => x.HoldType)
            .Must(type => type is AuditEntryHold.Legal or AuditEntryHold.Administrative)
            .WithMessage($"holdType must be '{AuditEntryHold.Legal}' or '{AuditEntryHold.Administrative}'.");

        RuleFor(x => x.ReasonCode)
            .Must(code => AuditEntryHold.ReasonCodes.Contains(code))
            .WithMessage("reasonCode must come from the ratified code set.");
    }
}

public sealed class ReleaseHoldCommandValidator : AbstractValidator<ReleaseHoldCommand>
{
    public ReleaseHoldCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.HoldId).NotEmpty();
    }
}

public sealed class PurgeExpiredCommandValidator : AbstractValidator<PurgeExpiredCommand>
{
    public PurgeExpiredCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();

        // Zero means "use the configured default batch size"; the handler
        // clamps the value into the configured [default, max] window.
        RuleFor(x => x.MaxBatchSize)
            .GreaterThanOrEqualTo(0)
            .WithMessage("maxBatchSize must not be negative.");
    }
}
