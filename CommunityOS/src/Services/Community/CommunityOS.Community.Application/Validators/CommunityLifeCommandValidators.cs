using CommunityOS.Community.Application.Commands;
using CommunityOS.Community.Application.Queries;
using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.SharedKernel.Domain.Primitives;
using FluentValidation;

namespace CommunityOS.Community.Application.Validators;

internal static class CommunityEnumRules
{
    public static bool IsKnown<T>(string? name) where T : Enumeration<int> =>
        !string.IsNullOrWhiteSpace(name) && IsKnown(typeof(T), name);

    private static bool IsKnown(Type enumerationType, string name)
    {
        var all = enumerationType.GetProperty("All", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null)
            as IEnumerable<Enumeration<int>>;
        return all?.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) ?? false;
    }

    public static bool ValidVisibility(string? name) => IsKnown<ActivityVisibility>(name);
    public static bool ValidEventVisibility(string? name) => IsKnown<CommunityEventVisibility>(name);
    public static bool ValidMeetingVisibility(string? name) => IsKnown<MeetingVisibility>(name);
    public static bool ValidPersonStatus(string? name) => IsKnown<PersonStatus>(name);
    public static bool ValidContactVisibility(string? name) => IsKnown<ContactVisibility>(name);
    public static bool ValidContactMethodType(string? name) => IsKnown<ContactMethodType>(name);
    public static bool ValidHouseholdRole(string? name) => IsKnown<HouseholdMemberRole>(name);
    public static bool ValidRelationshipType(string? name) => IsKnown<RelationshipType>(name);
    public static bool ValidMembershipStatus(string? name) => IsKnown<MembershipStatus>(name);
    public static bool ValidActivityStatus(string? name) => IsKnown<ActivityStatus>(name);
    public static bool ValidEventStatus(string? name) => IsKnown<CommunityEventStatus>(name);
    public static bool ValidMeetingStatus(string? name) => IsKnown<MeetingStatus>(name);
    public static bool ValidParticipationTarget(string? name) => IsKnown<ParticipationTargetType>(name);
    public static bool ValidParticipationStatus(string? name) => IsKnown<ParticipationStatus>(name);
}

public sealed class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.PreferredName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FormalName).MaximumLength(200);
        RuleFor(x => x.PreferredLanguage).MaximumLength(20);
        RuleFor(x => x.ProfileVisibility).Must(CommunityEnumRules.ValidContactVisibility);
        RuleFor(x => x.ContactVisibility).Must(CommunityEnumRules.ValidContactVisibility);
        RuleFor(x => x.DateOfBirthVisibility).Must(CommunityEnumRules.ValidContactVisibility);
    }
}

public sealed class UpdatePersonProfileCommandValidator : AbstractValidator<UpdatePersonProfileCommand>
{
    public UpdatePersonProfileCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.PreferredName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FormalName).MaximumLength(200);
        RuleFor(x => x.PreferredLanguage).MaximumLength(20);
    }
}

public sealed class SetPersonContactMethodsCommandValidator : AbstractValidator<SetPersonContactMethodsCommand>
{
    public SetPersonContactMethodsCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.ContactMethods).Must(methods =>
            methods.Count(m => m.IsPreferred) <= 1).WithMessage("Only one contact method may be preferred.");
        RuleForEach(x => x.ContactMethods).SetValidator(new ContactMethodInputValidator());
    }

    private sealed class ContactMethodInputValidator : AbstractValidator<ContactMethodInput>
    {
        public ContactMethodInputValidator()
        {
            RuleFor(x => x.Type).Must(CommunityEnumRules.ValidContactMethodType);
            RuleFor(x => x.Value).NotEmpty().MaximumLength(320);
            RuleFor(x => x.Visibility).Must(CommunityEnumRules.ValidContactVisibility);
        }
    }
}

public sealed class LinkPersonToIdentityAccountCommandValidator : AbstractValidator<LinkPersonToIdentityAccountCommand>
{
    public LinkPersonToIdentityAccountCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.IdentityAccountId).NotEmpty();
    }
}

public sealed class UnlinkPersonFromIdentityAccountCommandValidator : AbstractValidator<UnlinkPersonFromIdentityAccountCommand>
{
    public UnlinkPersonFromIdentityAccountCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
    }
}

public sealed class DeactivatePersonCommandValidator : AbstractValidator<DeactivatePersonCommand>
{
    public DeactivatePersonCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
    }
}

public sealed class CreateHouseholdCommandValidator : AbstractValidator<CreateHouseholdCommand>
{
    public CreateHouseholdCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(200);
        RuleFor(x => x.Country).MaximumLength(100);
    }
}

public sealed class UpdateHouseholdCommandValidator : AbstractValidator<UpdateHouseholdCommand>
{
    public UpdateHouseholdCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.HouseholdId).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(200);
    }
}

public sealed class AddHouseholdMemberCommandValidator : AbstractValidator<AddHouseholdMemberCommand>
{
    public AddHouseholdMemberCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.HouseholdId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.Role).Must(CommunityEnumRules.ValidHouseholdRole);
    }
}

public sealed class CreateFamilyRelationshipCommandValidator : AbstractValidator<CreateFamilyRelationshipCommand>
{
    public CreateFamilyRelationshipCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.PersonIdA).NotEmpty();
        RuleFor(x => x.PersonIdB).NotEmpty();
        RuleFor(x => x.RelationshipType).Must(CommunityEnumRules.ValidRelationshipType);
        RuleFor(x => x).Must(x => x.PersonIdA != x.PersonIdB).WithMessage("A person cannot be related to themselves.");
    }
}

public sealed class CreateMembershipCommandValidator : AbstractValidator<CreateMembershipCommand>
{
    public CreateMembershipCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.Status).Must(CommunityEnumRules.ValidMembershipStatus);
    }
}

public sealed class ChangeMembershipStatusCommandValidator : AbstractValidator<ChangeMembershipStatusCommand>
{
    public ChangeMembershipStatusCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.MembershipId).NotEmpty();
        RuleFor(x => x.Status).Must(CommunityEnumRules.ValidMembershipStatus);
    }
}

public sealed class CreateActivityCommandValidator : AbstractValidator<CreateActivityCommand>
{
    public CreateActivityCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.Visibility).Must(CommunityEnumRules.ValidVisibility);
        RuleFor(x => x.Status).Must(CommunityEnumRules.ValidActivityStatus);
        RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity is not null);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).When(x => x.EndsAt is not null);
    }
}

public sealed class UpdateActivityCommandValidator : AbstractValidator<UpdateActivityCommand>
{
    public UpdateActivityCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.ActivityId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Visibility).Must(CommunityEnumRules.ValidVisibility);
    }
}

public sealed class CreateCommunityEventCommandValidator : AbstractValidator<CreateCommunityEventCommand>
{
    public CreateCommunityEventCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Status).Must(CommunityEnumRules.ValidEventStatus);
        RuleFor(x => x.Visibility).Must(CommunityEnumRules.ValidEventVisibility);
        RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity is not null);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).When(x => x.EndsAt is not null);
    }
}

public sealed class UpdateCommunityEventCommandValidator : AbstractValidator<UpdateCommunityEventCommand>
{
    public UpdateCommunityEventCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Visibility).Must(CommunityEnumRules.ValidEventVisibility);
    }
}

public sealed class CreateMeetingCommandValidator : AbstractValidator<CreateMeetingCommand>
{
    public CreateMeetingCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Status).Must(CommunityEnumRules.ValidMeetingStatus);
        RuleFor(x => x.Visibility).Must(CommunityEnumRules.ValidMeetingVisibility);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).When(x => x.EndsAt is not null);
    }
}

public sealed class UpdateMeetingCommandValidator : AbstractValidator<UpdateMeetingCommand>
{
    public UpdateMeetingCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.MeetingId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Visibility).Must(CommunityEnumRules.ValidMeetingVisibility);
    }
}

public sealed class AddMeetingParticipantCommandValidator : AbstractValidator<AddMeetingParticipantCommand>
{
    public AddMeetingParticipantCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.MeetingId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.Role).NotEmpty().MaximumLength(100);
    }
}

public sealed class RecordParticipationCommandValidator : AbstractValidator<RecordParticipationCommand>
{
    public RecordParticipationCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.PersonId).NotEmpty();
        RuleFor(x => x.TargetType).Must(CommunityEnumRules.ValidParticipationTarget);
        RuleFor(x => x.TargetId).NotEmpty();
        RuleFor(x => x.Status).Must(CommunityEnumRules.ValidParticipationStatus);
        RuleFor(x => x.Role).MaximumLength(100);
    }
}

public sealed class GetCalendarQueryValidator : AbstractValidator<GetCalendarQuery>
{
    public GetCalendarQueryValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("'To' must be on or after 'From'.");
    }
}
