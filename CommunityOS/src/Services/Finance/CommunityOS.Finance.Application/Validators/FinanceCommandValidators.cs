using CommunityOS.Finance.Application.Commands;
using FluentValidation;

namespace CommunityOS.Finance.Application.Validators;

public sealed class CreateFundCommandValidator : AbstractValidator<CreateFundCommand>
{
    public CreateFundCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Z]{3}$");
    }
}

public sealed class CloseFundCommandValidator : AbstractValidator<CloseFundCommand>
{
    public CloseFundCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.FundId).NotEmpty();
    }
}

public sealed class RecordTransactionCommandValidator : AbstractValidator<RecordTransactionCommand>
{
    public RecordTransactionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.FundId).NotEmpty();
        RuleFor(x => x.Type).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Z]{3}$");
        RuleFor(x => x.MinorUnits).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.TransferDestinationFundId)
            .Must(v => !v.HasValue || v.Value != Guid.Empty)
            .WithMessage("A transfer destination must be a real fund reference.");
    }
}

public sealed class SubmitTransactionCommandValidator : AbstractValidator<SubmitTransactionCommand>
{
    public SubmitTransactionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
    }
}

public sealed class ApproveTransactionCommandValidator : AbstractValidator<ApproveTransactionCommand>
{
    public ApproveTransactionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
    }
}

public sealed class RejectTransactionCommandValidator : AbstractValidator<RejectTransactionCommand>
{
    public RejectTransactionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.TransactionId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}