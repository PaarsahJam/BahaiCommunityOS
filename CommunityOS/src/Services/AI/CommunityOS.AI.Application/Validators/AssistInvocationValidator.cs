using FluentValidation;

namespace CommunityOS.AI.Application.Commands;

internal sealed class AssistInvocationValidator : AbstractValidator<AssistInvocationCommand>
{
    public AssistInvocationValidator()
    {
        RuleFor(x => x.SubjectId)
            .NotEmpty()
            .WithMessage("Subject identifier is required.");

        RuleFor(x => x.Capability)
            .NotEmpty()
            .WithMessage("Capability is required.")
            .MaximumLength(100)
            .WithMessage("Capability must not exceed 100 characters.");

        RuleFor(x => x.Input)
            .NotEmpty()
            .WithMessage("Input is required.")
            .MaximumLength(100_000)
            .WithMessage("Input must not exceed 100,000 characters.");
    }
}
