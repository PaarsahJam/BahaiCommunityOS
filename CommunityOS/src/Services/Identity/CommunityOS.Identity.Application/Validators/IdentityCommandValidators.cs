using CommunityOS.Identity.Application.Commands;
using FluentValidation;

namespace CommunityOS.Identity.Application.Validators;

public sealed class RegisterMemberCommandValidator : AbstractValidator<RegisterMemberCommand>
{
    public RegisterMemberCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.PhoneNumber).MaximumLength(20).When(x => x.PhoneNumber is not null);
    }
}

public sealed class UpdateMemberContactCommandValidator : AbstractValidator<UpdateMemberContactCommand>
{
    public UpdateMemberContactCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.PhoneNumber).MaximumLength(20).When(x => x.PhoneNumber is not null);
        RuleFor(x => x.CountryCode).Length(2).When(x => x.CountryCode is not null);
    }
}

public sealed class SuspendMemberCommandValidator : AbstractValidator<SuspendMemberCommand>
{
    public SuspendMemberCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class TransferMemberCommandValidator : AbstractValidator<TransferMemberCommand>
{
    public TransferMemberCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.TargetLocalUnitId).NotEmpty();
    }
}

public sealed class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.RoleName).NotEmpty().MaximumLength(100);
    }
}
