using CommunityOS.Community.Application.Commands;
using FluentValidation;

namespace CommunityOS.Community.Application.Validators;

public sealed class CreateCommunityCommandValidator : AbstractValidator<CreateCommunityCommand>
{
    public CreateCommunityCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.HierarchyLevelId).InclusiveBetween(1, 4);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
    }
}

public sealed class UpdateCommunityCommandValidator : AbstractValidator<UpdateCommunityCommand>
{
    public UpdateCommunityCommandValidator()
    {
        RuleFor(x => x.CommunityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
    }
}

public sealed class AddLocalUnitCommandValidator : AbstractValidator<AddLocalUnitCommand>
{
    public AddLocalUnitCommandValidator()
    {
        RuleFor(x => x.CommunityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ClusterId).NotEmpty();
    }
}

public sealed class ChangeCommunityParentCommandValidator : AbstractValidator<ChangeCommunityParentCommand>
{
    public ChangeCommunityParentCommandValidator()
    {
        RuleFor(x => x.CommunityId).NotEmpty();
    }
}
