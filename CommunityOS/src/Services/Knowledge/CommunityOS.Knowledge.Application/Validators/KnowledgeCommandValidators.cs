using CommunityOS.Knowledge.Application.Commands;
using FluentValidation;

namespace CommunityOS.Knowledge.Application.Validators;

public sealed class CreateWorkCommandValidator : AbstractValidator<CreateWorkCommand>
{
    public CreateWorkCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.WorkType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.OriginalLanguage).NotEmpty().MaximumLength(20);
        RuleFor(x => x.DefaultLanguage).NotEmpty().MaximumLength(20);
    }
}

public sealed class UpdateWorkCommandValidator : AbstractValidator<UpdateWorkCommand>
{
    public UpdateWorkCommandValidator()
    {
        RuleFor(x => x.WorkId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.WorkType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.OriginalLanguage).NotEmpty().MaximumLength(20);
        RuleFor(x => x.DefaultLanguage).NotEmpty().MaximumLength(20);
    }
}

public sealed class ImportEditionCommandValidator : AbstractValidator<ImportEditionCommand>
{
    public ImportEditionCommandValidator()
    {
        RuleFor(x => x.WorkId).NotEmpty();
        RuleFor(x => x.Language).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Translator).MaximumLength(200);
        RuleFor(x => x.Publisher).MaximumLength(300);
    }
}

public sealed class ImportPassageCommandValidator : AbstractValidator<ImportPassageCommand>
{
    public ImportPassageCommandValidator()
    {
        RuleFor(x => x.EditionId).NotEmpty();
        RuleFor(x => x.ReferencePath).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(20000);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class CorrectPassageCommandValidator : AbstractValidator<CorrectPassageCommand>
{
    public CorrectPassageCommandValidator()
    {
        RuleFor(x => x.PassageId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(20000);
    }
}

public sealed class CreateQuestionCommandValidator : AbstractValidator<CreateQuestionCommand>
{
    public CreateQuestionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
    }
}

public sealed class FlagQuestionCommandValidator : AbstractValidator<FlagQuestionCommand>
{
    public FlagQuestionCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class CreateAnswerCommandValidator : AbstractValidator<CreateAnswerCommand>
{
    public CreateAnswerCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(20000);
    }
}

public sealed class UpdateAnswerCommandValidator : AbstractValidator<UpdateAnswerCommand>
{
    public UpdateAnswerCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.AnswerId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(20000);
    }
}

public sealed class CreateDiscussionCommandValidator : AbstractValidator<CreateDiscussionCommand>
{
    public CreateDiscussionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
    }
}

public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.DiscussionId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
    }
}

public sealed class RequestAiSuggestionCommandValidator : AbstractValidator<RequestAiSuggestionCommand>
{
    public RequestAiSuggestionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.ModelId).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreateTopicCommandValidator : AbstractValidator<CreateTopicCommand>
{
    public CreateTopicCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}