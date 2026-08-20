using CommunityOS.Notifications.Application.Commands;
using FluentValidation;

namespace CommunityOS.Notifications.Application.Validators;

public sealed class CreateNotificationCommandValidator : AbstractValidator<CreateNotificationCommand>
{
    public CreateNotificationCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.TypeCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Channel).NotEmpty().MaximumLength(20);
        RuleFor(x => x.SourceType).MaximumLength(50);
        RuleFor(x => x.SourceId).NotEmpty().When(x => x.SourceId.HasValue);
        RuleFor(x => x.SourceType).NotEmpty().When(x => x.SourceId.HasValue);
        RuleFor(x => x.SourceId).Null().When(x => x.SourceType is null);
        RuleFor(x => x.OrganizationUnitId).NotEmpty().When(x => x.OrganizationUnitId.HasValue);
        RuleFor(x => x.ScheduledFor).NotNull().When(x => x.ScheduledFor.HasValue);
        RuleFor(x => x.AdditionalScopes).NotNull();
        RuleFor(x => x.RecipientIds).NotNull().NotEmpty();
        RuleForEach(x => x.RecipientIds).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
    }
}

public sealed class DispatchNotificationCommandValidator : AbstractValidator<DispatchNotificationCommand>
{
    public DispatchNotificationCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.NotificationId).NotEmpty();
    }
}

public sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.NotificationId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
    }
}

public sealed class CreateNotificationTypeCommandValidator : AbstractValidator<CreateNotificationTypeCommand>
{
    public CreateNotificationTypeCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DefaultChannel).NotEmpty().MaximumLength(20);
        RuleFor(x => x.SubjectTemplate).NotEmpty().MaximumLength(500);
        RuleFor(x => x.BodyTemplate).NotEmpty().MaximumLength(2000);
    }
}

public sealed class UpdateNotificationTypeCommandValidator : AbstractValidator<UpdateNotificationTypeCommand>
{
    public UpdateNotificationTypeCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DefaultChannel).NotEmpty().MaximumLength(20);
        RuleFor(x => x.SubjectTemplate).NotEmpty().MaximumLength(500);
        RuleFor(x => x.BodyTemplate).NotEmpty().MaximumLength(2000);
    }
}

public sealed class RetireNotificationTypeCommandValidator : AbstractValidator<RetireNotificationTypeCommand>
{
    public RetireNotificationTypeCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdateMemberPreferencesCommandValidator : AbstractValidator<UpdateMemberPreferencesCommand>
{
    public UpdateMemberPreferencesCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Rules).NotNull();
        RuleForEach(x => x.Rules)
            .ChildRules(rule =>
            {
                rule.RuleFor(r => r.TypeCode).NotEmpty().MaximumLength(100);
                rule.RuleFor(r => r.Channels).NotNull().NotEmpty();
                rule.RuleForEach(r => r.Channels).NotEmpty().MaximumLength(20);
            });
    }
}