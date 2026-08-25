using CommunityOS.Localization.Domain;
using FluentValidation;

namespace CommunityOS.Localization.Application.Validators;

public sealed class CreateLocaleValidator : AbstractValidator<CreateLocaleCommand>
{
    public CreateLocaleValidator()
    {
        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(c => Bcp47.IsValid(c))
            .WithMessage("Code must be a valid BCP-47 culture code.");
        RuleFor(x => x.DisplayName).MaximumLength(200);
    }
}

public sealed class ActivateLocaleValidator : AbstractValidator<ActivateLocaleCommand>
{
    public ActivateLocaleValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(35);
    }
}

public sealed class DeactivateLocaleValidator : AbstractValidator<DeactivateLocaleCommand>
{
    public DeactivateLocaleValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(35);
    }
}

public sealed class CreateNamespaceValidator : AbstractValidator<CreateNamespaceCommand>
{
    private const string AllowedChars = "abcdefghijklmnopqrstuvwxyz0123456789._-";

    public CreateNamespaceValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Length(3, 100)
            .Must(n => n.Trim().ToLowerInvariant().All(AllowedChars.Contains))
            .WithMessage("Namespace names may contain lowercase letters, digits, '.', '_' and '-' only.");
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class WalkEntriesValidator : AbstractValidator<WalkEntriesQuery>
{
    private static readonly HashSet<string> States = new(StringComparer.Ordinal)
    {
        "draft", "in_review", "approved", "rejected", "superseded", "deprecated"
    };

    public WalkEntriesValidator()
    {
        RuleFor(x => x.Limit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.State)
            .Must(s => string.IsNullOrWhiteSpace(s) || States.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Unknown state filter.");
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.Cursor).MaximumLength(512);
    }
}

public sealed class CreateResourceEntryValidator : AbstractValidator<CreateResourceEntryCommand>
{
    public CreateResourceEntryValidator()
    {
        RuleFor(x => x.NamespaceId).NotEqual(Guid.Empty);
        RuleFor(x => x.Key)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().MaximumLength(ResourceEntry.KeyMaxLength)
            .Must(k => k.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ':'))
            .WithMessage("Resource keys may contain ASCII letters, digits and '.', '_', '-', ':' only.");
        RuleFor(x => x.Culture).Must(Bcp47.IsValid)
            .WithMessage("Culture must be a valid BCP-47 culture code.");
        RuleFor(x => x.Value).NotEmpty().MaximumLength(ResourceRevision.ValueMaxLength);
    }
}

public sealed class AddResourceRevisionValidator : AbstractValidator<AddResourceRevisionCommand>
{
    public AddResourceRevisionValidator()
    {
        RuleFor(x => x.EntryId).NotEqual(Guid.Empty);
        RuleFor(x => x.Culture).Must(Bcp47.IsValid)
            .WithMessage("Culture must be a valid BCP-47 culture code.");
        RuleFor(x => x.Value).NotEmpty().MaximumLength(ResourceRevision.ValueMaxLength);
    }
}

public sealed class SubmitRevisionValidator : AbstractValidator<SubmitRevisionCommand>
{
    public SubmitRevisionValidator()
    {
        RuleFor(x => x.EntryId).NotEqual(Guid.Empty);
        RuleFor(x => x.RevisionId).NotEqual(Guid.Empty);
    }
}

public sealed class ApproveRevisionValidator : AbstractValidator<ApproveRevisionCommand>
{
    public ApproveRevisionValidator()
    {
        RuleFor(x => x.EntryId).NotEqual(Guid.Empty);
        RuleFor(x => x.RevisionId).NotEqual(Guid.Empty);
    }
}

public sealed class RejectRevisionValidator : AbstractValidator<RejectRevisionCommand>
{
    public RejectRevisionValidator()
    {
        RuleFor(x => x.EntryId).NotEqual(Guid.Empty);
        RuleFor(x => x.RevisionId).NotEqual(Guid.Empty);
    }
}

public sealed class DeprecateResourceEntryValidator : AbstractValidator<DeprecateResourceEntryCommand>
{
    public DeprecateResourceEntryValidator()
    {
        RuleFor(x => x.EntryId).NotEqual(Guid.Empty);
    }
}

public sealed class ExportBundleValidator : AbstractValidator<ExportBundleQuery>
{
    public ExportBundleValidator()
    {
        RuleFor(x => x.Culture).Must(Bcp47.IsValid)
            .WithMessage("Culture must be a valid BCP-47 culture code.");
    }
}

public sealed class UpsertEntityTranslationsValidator : AbstractValidator<UpsertEntityTranslationsCommand>
{
    private const int HardBatchLimit = 200;

    public UpsertEntityTranslationsValidator()
    {
        RuleFor(x => x.Items).NotNull().NotEmpty();
        RuleFor(x => x.Items.Count).LessThanOrEqualTo(HardBatchLimit)
            .WithMessage($"Batch upserts are limited to {HardBatchLimit} items.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.SourceContext)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().Length(2, 50)
                .Must(s => s.All(char.IsAsciiLetterOrDigit) || s.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                .WithMessage("SourceContext may contain letters, digits and underscores only.");
            item.RuleFor(i => i.EntityType)
                .NotEmpty().Length(2, 100);
            item.RuleFor(i => i.EntityId).NotEqual(Guid.Empty);
            item.RuleFor(i => i.Field)
                .NotEmpty().Length(2, 100);
            item.RuleFor(i => i.Culture).Must(Bcp47.IsValid)
                .WithMessage("Culture must be a valid BCP-47 culture code.");
            item.RuleFor(i => i.Value).NotEmpty().MaximumLength(ResourceRevision.ValueMaxLength);
        });
    }
}

public sealed class QueryEntityTranslationsValidator : AbstractValidator<QueryEntityTranslationsCommand>
{
    private const int HardBatchLimit = 200;

    public QueryEntityTranslationsValidator()
    {
        RuleFor(x => x.Refs).NotNull().NotEmpty();
        RuleFor(x => x.Refs.Count).LessThanOrEqualTo(HardBatchLimit)
            .WithMessage($"Batch queries are limited to {HardBatchLimit} items.");
        RuleForEach(x => x.Refs).ChildRules(item =>
        {
            item.RuleFor(i => i.SourceContext).NotEmpty().Length(2, 50);
            item.RuleFor(i => i.EntityType).NotEmpty().Length(2, 100);
            item.RuleFor(i => i.EntityId).NotEqual(Guid.Empty);
            item.RuleFor(i => i.Field).NotEmpty().Length(2, 100);
            item.RuleFor(i => i.Culture).Must(Bcp47.IsValid)
                .WithMessage("Culture must be a valid BCP-47 culture code.");
        });
    }
}

public sealed class ListSuggestionsValidator : AbstractValidator<ListSuggestionsQuery>
{
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "pending", "accepted_into_review", "rejected"
    };

    public ListSuggestionsValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || Statuses.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Unknown status filter.");
        RuleFor(x => x.Limit).GreaterThanOrEqualTo(0);
    }
}

public sealed class AcceptSuggestionValidator : AbstractValidator<AcceptSuggestionIntoReviewCommand>
{
    public AcceptSuggestionValidator()
    {
        RuleFor(x => x.SuggestionId).NotEqual(Guid.Empty);
    }
}

public sealed class RejectSuggestionValidator : AbstractValidator<RejectSuggestionCommand>
{
    public RejectSuggestionValidator()
    {
        RuleFor(x => x.SuggestionId).NotEqual(Guid.Empty);
    }
}
