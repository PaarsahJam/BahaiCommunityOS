using CommunityOS.Documents.Application.Commands;
using FluentValidation;

namespace CommunityOS.Documents.Application.Validators;

public sealed class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.OwnerType).MaximumLength(30);
        RuleFor(x => x.OwnerId).NotEmpty().When(x => x.OwnerType is not null);
        RuleFor(x => x.OrganizationUnitId).NotEmpty().When(x => x.OrganizationUnitId.HasValue);
    }
}

public sealed class UpdateDocumentMetadataCommandValidator : AbstractValidator<UpdateDocumentMetadataCommand>
{
    public UpdateDocumentMetadataCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class ClassifyDocumentCommandValidator : AbstractValidator<ClassifyDocumentCommand>
{
    public ClassifyDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.ClassificationCode).MaximumLength(50);
        RuleFor(x => x.RetentionCategory).MaximumLength(100);
        RuleFor(x => x.LegalHoldReference).MaximumLength(100);
        RuleFor(x => x.AdministrativeHoldReference).MaximumLength(100);
    }
}

public sealed class AddDocumentScopeCommandValidator : AbstractValidator<AddDocumentScopeCommand>
{
    public AddDocumentScopeCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
    }
}

public sealed class RemoveDocumentScopeCommandValidator : AbstractValidator<RemoveDocumentScopeCommand>
{
    public RemoveDocumentScopeCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.OrganizationUnitId).NotEmpty();
    }
}

public sealed class UploadDocumentVersionCommandValidator : AbstractValidator<UploadDocumentVersionCommand>
{
    public UploadDocumentVersionCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Length).GreaterThan(0);
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.AllowedMimeTypes).NotNull();
    }
}

public sealed class DeactivateDocumentCommandValidator : AbstractValidator<DeactivateDocumentCommand>
{
    public DeactivateDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class RestoreDocumentCommandValidator : AbstractValidator<RestoreDocumentCommand>
{
    public RestoreDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
    }
}

public sealed class ArchiveDocumentCommandValidator : AbstractValidator<ArchiveDocumentCommand>
{
    public ArchiveDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
    }
}

public sealed class CreateDocumentReferenceCommandValidator : AbstractValidator<CreateDocumentReferenceCommand>
{
    public CreateDocumentReferenceCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.SourceContext).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SourceEntityId).NotEmpty();
        RuleFor(x => x.ReferenceType).NotEmpty().MaximumLength(50);
    }
}