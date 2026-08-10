using CommunityOS.Authorization.Application.Commands;
using CommunityOS.Authorization.Domain.Enumerations;
using CommunityOS.Authorization.Domain.Permissions;
using CommunityOS.Authorization.Domain.Relationships;
using FluentValidation;

namespace CommunityOS.Authorization.Application.Validators;

internal static class ScopeRules
{
    public static readonly string[] KnownScopeTypes = ScopeType.All.Select(t => t.Name).ToArray();

    public static bool IsKnownScopeType(string? scopeType) =>
        !string.IsNullOrWhiteSpace(scopeType) &&
        KnownScopeTypes.Any(t => string.Equals(t, scopeType, StringComparison.OrdinalIgnoreCase));

    public static bool ScopeIdRequired(string? scopeType, Guid? scopeId) =>
        string.Equals(scopeType, ScopeType.Global.Name, StringComparison.OrdinalIgnoreCase) ||
        scopeId is not null;

    public static bool ResourceTypeRequired(string? scopeType, string? resourceType) =>
        !string.Equals(scopeType, ScopeType.Resource.Name, StringComparison.OrdinalIgnoreCase) ||
        !string.IsNullOrWhiteSpace(resourceType);
}

internal static class PermissionRules
{
    public static bool IsValidPermission(string? permission) =>
        !string.IsNullOrWhiteSpace(permission) && PermissionName.IsValid(permission);
}

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Permissions).NotEmpty();
        RuleForEach(x => x.Permissions).Must(PermissionRules.IsValidPermission);
    }
}

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Permissions).NotEmpty();
        RuleForEach(x => x.Permissions).Must(PermissionRules.IsValidPermission);
    }
}

public sealed class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.ScopeType).Must(ScopeRules.IsKnownScopeType)
            .WithMessage("'{PropertyName}' must be one of: {KnownScopeTypes}.");
        RuleFor(x => x.ScopeId).Must((cmd, scopeId) => ScopeRules.ScopeIdRequired(cmd.ScopeType, scopeId))
            .WithMessage("'{PropertyName}' is required when the scope is not Global.");
        RuleFor(x => x.ResourceType).Must((cmd, resourceType) => ScopeRules.ResourceTypeRequired(cmd.ScopeType, resourceType))
            .WithMessage("'{PropertyName}' is required when the scope type is Resource.");
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x).Must(x => x.EffectiveUntil is null ||
                                  x.EffectiveFrom is null ||
                                  x.EffectiveUntil > x.EffectiveFrom)
            .WithMessage("'EffectiveUntil' must be later than 'EffectiveFrom'.");
    }
}

public sealed class RevokeRoleCommandValidator : AbstractValidator<RevokeRoleCommand>
{
    public RevokeRoleCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class CheckPermissionCommandValidator : AbstractValidator<CheckPermissionCommand>
{
    public CheckPermissionCommandValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Permission).Must(PermissionRules.IsValidPermission);
        RuleFor(x => x.CheckKey).MaximumLength(100);
    }
}

public sealed class BatchCheckPermissionCommandValidator : AbstractValidator<BatchCheckPermissionCommand>
{
    public BatchCheckPermissionCommandValidator()
    {
        RuleFor(x => x.Requests).NotEmpty().Must(list => list.Count <= 100)
            .WithMessage("'{PropertyName}' may contain at most 100 items.");
        RuleForEach(x => x.Requests).SetValidator(new CheckRequestItemValidator());
    }

    private sealed class CheckRequestItemValidator : AbstractValidator<CheckRequestItem>
    {
        public CheckRequestItemValidator()
        {
            RuleFor(x => x.SubjectId).NotEmpty();
            RuleFor(x => x.Permission).Must(PermissionRules.IsValidPermission);
            RuleFor(x => x.CheckKey).MaximumLength(100);
        }
    }
}

public sealed class WriteRelationshipCommandValidator : AbstractValidator<WriteRelationshipCommand>
{
    public WriteRelationshipCommandValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Relation).NotEmpty()
            .Must(RelationName.IsValid).WithMessage("'{PropertyName}' is not a known relationship name.");
        RuleFor(x => x.ObjectType).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ObjectId).NotEmpty();
        RuleForEach(x => x.Permissions).Must(PermissionRules.IsValidPermission);
    }
}

public sealed class ReadRelationshipsQueryValidator : AbstractValidator<ReadRelationshipsQuery>
{
    public ReadRelationshipsQueryValidator()
    {
        RuleFor(x => x.Relation).Must(rel => rel is null || RelationName.IsValid(rel))
            .WithMessage("'{PropertyName}' is not a known relationship name.");
    }
}

public sealed class GrantDelegationCommandValidator : AbstractValidator<GrantDelegationCommand>
{
    public GrantDelegationCommandValidator()
    {
        RuleFor(x => x.DelegateId).NotEmpty();
        RuleFor(x => x.Permissions).NotEmpty();
        RuleForEach(x => x.Permissions).Must(PermissionRules.IsValidPermission);
        RuleFor(x => x.ScopeType).Must(ScopeRules.IsKnownScopeType)
            .WithMessage("'{PropertyName}' must be one of: {KnownScopeTypes}.");
        RuleFor(x => x.ScopeId).Must((cmd, scopeId) => ScopeRules.ScopeIdRequired(cmd.ScopeType, scopeId))
            .WithMessage("'{PropertyName}' is required when the scope is not Global.");
        RuleFor(x => x.ResourceType).Must((cmd, resourceType) => ScopeRules.ResourceTypeRequired(cmd.ScopeType, resourceType))
            .WithMessage("'{PropertyName}' is required when the scope type is Resource.");
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x).Must(x => x.ExpiresOn is null || x.StartsOn is null || x.ExpiresOn > x.StartsOn)
            .WithMessage("'ExpiresOn' must be later than 'StartsOn'.");
    }
}

public sealed class RevokeDelegationCommandValidator : AbstractValidator<RevokeDelegationCommand>
{
    public RevokeDelegationCommandValidator()
    {
        RuleFor(x => x.DelegationId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class RequestBreakGlassCommandValidator : AbstractValidator<RequestBreakGlassCommand>
{
    public RequestBreakGlassCommandValidator()
    {
        RuleFor(x => x.Permissions).NotEmpty();
        RuleForEach(x => x.Permissions).Must(PermissionRules.IsValidPermission);
        RuleFor(x => x.ScopeType).Must(ScopeRules.IsKnownScopeType)
            .WithMessage("'{PropertyName}' must be one of: {KnownScopeTypes}.");
        RuleFor(x => x.ScopeId).Must((cmd, scopeId) => ScopeRules.ScopeIdRequired(cmd.ScopeType, scopeId))
            .WithMessage("'{PropertyName}' is required when the scope is not Global.");
        RuleFor(x => x.ResourceType).Must((cmd, resourceType) => ScopeRules.ResourceTypeRequired(cmd.ScopeType, resourceType))
            .WithMessage("'{PropertyName}' is required when the scope type is Resource.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.RequestedDurationMinutes).GreaterThan(0);
    }
}

public sealed class ApproveBreakGlassCommandValidator : AbstractValidator<ApproveBreakGlassCommand>
{
    public ApproveBreakGlassCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
    }
}

public sealed class RejectBreakGlassCommandValidator : AbstractValidator<RejectBreakGlassCommand>
{
    public RejectBreakGlassCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}

public sealed class RevokeBreakGlassCommandValidator : AbstractValidator<RevokeBreakGlassCommand>
{
    public RevokeBreakGlassCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}
