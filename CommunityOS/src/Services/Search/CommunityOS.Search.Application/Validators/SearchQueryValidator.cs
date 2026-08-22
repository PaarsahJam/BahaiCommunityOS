using CommunityOS.Search.Domain;
using FluentValidation;

namespace CommunityOS.Search.Application.Validators;

public sealed class SearchQueryValidator : AbstractValidator<SearchQuery>
{
    public SearchQueryValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();

        RuleFor(x => x.Query)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(q => q.Trim().Length > 0)
                .WithMessage("Query is required.")
                .WithErrorCode("SearchQueryRequired");

        RuleFor(x => x.SourceType)
            .Must(type => type is null || SearchSourceTypes.All.Contains(type))
            .When(x => x.SourceType is not null)
            .WithMessage("Unknown search source type '{PropertyValue}'.");

        RuleFor(x => x.Types)
            .Must(types => types is null || types.All(t => SearchSourceTypes.All.Contains(t)))
            .WithMessage("Query contains an unknown search source type.");

        RuleFor(x => x.Limit)
            .GreaterThanOrEqualTo(1)
            .When(x => x.Limit.HasValue)
            .WithMessage("limit must be greater than or equal to 1.");

        RuleFor(x => x.Offset)
            .GreaterThanOrEqualTo(0)
            .WithMessage("offset must be greater than or equal to 0.");

        RuleForEach(x => x.Statuses)
            .ChildRules(status =>
            {
                status.RuleFor(s => s).NotEmpty().MaximumLength(50);
            });
    }
}

public sealed class ReindexCommandValidator : AbstractValidator<ReindexCommand>
{
    public ReindexCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();

        RuleFor(x => x.SourceType)
            .Must(type => type is null || SearchSourceTypes.All.Contains(type))
            .When(x => x.SourceType is not null)
            .WithMessage("Unknown search source type '{PropertyValue}'.");
    }
}
