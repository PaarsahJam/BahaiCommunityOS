using CommunityOS.Workflow.Application.Commands;
using FluentValidation;

namespace CommunityOS.Workflow.Application.Validators;

public sealed class CreateWorkflowTaskCommandValidator : AbstractValidator<CreateWorkflowTaskCommand>
{
    public CreateWorkflowTaskCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.DefinitionCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DomainType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.OrganizationUnitId).NotEmpty().When(x => x.OrganizationUnitId.HasValue);
        RuleFor(x => x.DueOn).NotNull().When(x => x.DueOn.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.AdditionalScopes).NotNull();
        RuleFor(x => x.AssigneeIds).NotNull();
    }
}

public sealed class AssignWorkflowTaskCommandValidator : AbstractValidator<AssignWorkflowTaskCommand>
{
    public AssignWorkflowTaskCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.AssigneeIds).NotNull().NotEmpty();
    }
}

public sealed class StartWorkflowTaskCommandValidator : AbstractValidator<StartWorkflowTaskCommand>
{
    public StartWorkflowTaskCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.TaskId).NotEmpty();
    }
}

public sealed class CompleteWorkflowTaskCommandValidator : AbstractValidator<CompleteWorkflowTaskCommand>
{
    public CompleteWorkflowTaskCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Outcome).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CancelWorkflowTaskCommandValidator : AbstractValidator<CancelWorkflowTaskCommand>
{
    public CancelWorkflowTaskCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.TaskId).NotEmpty();
    }
}

public sealed class EscalateWorkflowTaskCommandValidator : AbstractValidator<EscalateWorkflowTaskCommand>
{
    public EscalateWorkflowTaskCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.EscalateTo).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class CreateTaskDefinitionCommandValidator : AbstractValidator<CreateTaskDefinitionCommand>
{
    public CreateTaskDefinitionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.DomainType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PermittedOutcomes).NotNull().NotEmpty();
        RuleForEach(x => x.PermittedOutcomes)
            .NotEmpty().MaximumLength(50);
        RuleFor(x => x.DueIn).MaximumLength(30);
    }
}

public sealed class UpdateTaskDefinitionCommandValidator : AbstractValidator<UpdateTaskDefinitionCommand>
{
    public UpdateTaskDefinitionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.DomainType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PermittedOutcomes).NotNull().NotEmpty();
        RuleForEach(x => x.PermittedOutcomes)
            .NotEmpty().MaximumLength(50);
        RuleFor(x => x.DueIn).MaximumLength(30);
    }
}

public sealed class RetireTaskDefinitionCommandValidator : AbstractValidator<RetireTaskDefinitionCommand>
{
    public RetireTaskDefinitionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    }
}