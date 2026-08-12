using CommunityOS.Organization.Application.Commands;
using CommunityOS.Organization.Domain.Enumerations;
using FluentValidation;

namespace CommunityOS.Organization.Application.Validators;

internal static class OrganizationTypeRules
{
    public static bool IsKnownJurisdictionType(string? jurisdictionType) =>
        !string.IsNullOrWhiteSpace(jurisdictionType) &&
        JurisdictionType.All.Any(t => string.Equals(
            t.Name, jurisdictionType, StringComparison.OrdinalIgnoreCase));
}

public sealed class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OrganizationType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.JurisdictionType)
            .NotEmpty()
            .Must(OrganizationTypeRules.IsKnownJurisdictionType);
        RuleFor(x => x.JurisdictionScopeId)
            .NotEmpty()
            .When(x => !string.Equals(
                x.JurisdictionType, JurisdictionType.Global.Name, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class UpdateOrganizationCommandValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.JurisdictionType)
            .NotEmpty()
            .Must(OrganizationTypeRules.IsKnownJurisdictionType);
    }
}

public sealed class DissolveOrganizationCommandValidator : AbstractValidator<DissolveOrganizationCommand>
{
    public DissolveOrganizationCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.OrganizationId).NotEmpty();
    }
}

public sealed class CreateOrganizationUnitCommandValidator : AbstractValidator<CreateOrganizationUnitCommand>
{
    public CreateOrganizationUnitCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UnitType).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdateOrganizationUnitCommandValidator : AbstractValidator<UpdateOrganizationUnitCommand>
{
    public UpdateOrganizationUnitCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UnitType).NotEmpty().MaximumLength(100);
    }
}

public sealed class ChangeOrganizationUnitParentCommandValidator
    : AbstractValidator<ChangeOrganizationUnitParentCommand>
{
    public ChangeOrganizationUnitParentCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
        RuleFor(x => x.ParentId).NotEqual(x => x.OrganizationUnitId).When(x => x.ParentId is not null);
        RuleFor(x => x.EffectiveUntil)
            .GreaterThan(x => x.EffectiveFrom)
            .When(x => x.EffectiveUntil is not null);
    }
}

public sealed class DeactivateOrganizationUnitCommandValidator
    : AbstractValidator<DeactivateOrganizationUnitCommand>
{
    public DeactivateOrganizationUnitCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
    }
}

public sealed class AssignAppointmentCommandValidator : AbstractValidator<AssignAppointmentCommand>
{
    public AssignAppointmentCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
        RuleFor(x => x.AppointmentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EffectiveUntil)
            .GreaterThan(x => x.EffectiveFrom)
            .When(x => x.EffectiveUntil is not null);
        RuleFor(x => x.Reason).MaximumLength(500).When(x => x.Reason is not null);
    }
}

public sealed class EndAppointmentCommandValidator : AbstractValidator<EndAppointmentCommand>
{
    public EndAppointmentCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.AppointmentId).NotEmpty();
    }
}

public sealed class CreateCommitteeCommandValidator : AbstractValidator<CreateCommitteeCommand>
{
    public CreateCommitteeCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CommitteeType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.JurisdictionType)
            .NotEmpty()
            .Must(OrganizationTypeRules.IsKnownJurisdictionType);
    }
}

public sealed class UpdateCommitteeCommandValidator : AbstractValidator<UpdateCommitteeCommand>
{
    public UpdateCommitteeCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.CommitteeId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.JurisdictionType)
            .NotEmpty()
            .Must(OrganizationTypeRules.IsKnownJurisdictionType);
    }
}

public sealed class AddCommitteeMemberCommandValidator : AbstractValidator<AddCommitteeMemberCommand>
{
    public AddCommitteeMemberCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.CommitteeId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.RoleCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EffectiveUntil)
            .GreaterThan(x => x.EffectiveFrom)
            .When(x => x.EffectiveUntil is not null);
    }
}

public sealed class RemoveCommitteeMemberCommandValidator : AbstractValidator<RemoveCommitteeMemberCommand>
{
    public RemoveCommitteeMemberCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.CommitteeId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.RoleCode).NotEmpty().MaximumLength(100);
    }
}

public sealed class DeactivateCommitteeCommandValidator : AbstractValidator<DeactivateCommitteeCommand>
{
    public DeactivateCommitteeCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.CommitteeId).NotEmpty();
    }
}

public sealed class CreateInstitutionCommandValidator : AbstractValidator<CreateInstitutionCommand>
{
    public CreateInstitutionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.InstitutionType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.JurisdictionType)
            .NotEmpty()
            .Must(OrganizationTypeRules.IsKnownJurisdictionType);
    }
}

public sealed class UpdateInstitutionCommandValidator : AbstractValidator<UpdateInstitutionCommand>
{
    public UpdateInstitutionCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.InstitutionId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.JurisdictionType)
            .NotEmpty()
            .Must(OrganizationTypeRules.IsKnownJurisdictionType);
    }
}

public sealed class GrantDelegationFactCommandValidator : AbstractValidator<GrantDelegationFactCommand>
{
    public GrantDelegationFactCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.DelegatorId).NotEmpty();
        RuleFor(x => x.DelegateId).NotEmpty();
        RuleFor(x => x.DelegatorId).NotEqual(x => x.DelegateId);
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
        RuleFor(x => x.DelegationType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EffectiveUntil)
            .GreaterThan(x => x.EffectiveFrom)
            .When(x => x.EffectiveUntil is not null);
    }
}

public sealed class RevokeDelegationFactCommandValidator : AbstractValidator<RevokeDelegationFactCommand>
{
    public RevokeDelegationFactCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.DelegationFactId).NotEmpty();
    }
}