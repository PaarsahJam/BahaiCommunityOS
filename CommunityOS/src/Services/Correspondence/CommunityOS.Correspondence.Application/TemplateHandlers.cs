using CommunityOS.Correspondence.Domain;
using CommunityOS.Correspondence.Domain.Exceptions;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using MediatR;

namespace CommunityOS.Correspondence.Application;

public sealed record ListTemplates(Guid ActorId) : IRequest<IReadOnlyList<TemplateDto>>;

public sealed record TemplateDto(
    Guid Id, string Code, string Title, string Category,
    bool IsActive, DateTime CreatedOn, DateTime UpdatedOn);

public sealed class ListTemplatesHandler(
    ILetterReader reader,
    AuthorizationGuard guard)
    : IRequestHandler<ListTemplates, IReadOnlyList<TemplateDto>>
{
    public async Task<IReadOnlyList<TemplateDto>> Handle(ListTemplates request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, Permissions.LetterPermissions.TemplateRead, ct: cancellationToken);

        var templates = await reader.ListActiveTemplatesAsync(cancellationToken);
        return templates
            .Select(t => new TemplateDto(t.Id, t.Code, t.Title, t.CategoryCode, t.IsActive, t.CreatedOn, t.UpdatedOn))
            .ToList();
    }
}

public sealed record CreateTemplateCommand(
    Guid ActorId, string Code, string Title, string SubjectTemplate, string BodyTemplate, string Category)
    : IRequest<TemplateDto>;

public sealed class CreateTemplateHandler(
    ILetterReader reader,
    ILetterJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<CreateTemplateCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(CreateTemplateCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, Permissions.LetterPermissions.TemplateManage, ct: cancellationToken);

        if (await reader.TemplateCodeExistsAsync(command.Code.Trim(), cancellationToken))
        {
            throw new LetterConflictException($"A template with code '{command.Code.Trim()}' already exists.");
        }

        var template = Template.Create(
            command.Code, command.Title, command.SubjectTemplate, command.BodyTemplate,
            command.Category, command.ActorId, DateTime.UtcNow);
        await journal.SaveTemplateAsync(template, cancellationToken);
        return new(template.Id, template.Code, template.Title, template.CategoryCode,
            template.IsActive, template.CreatedOn, template.UpdatedOn);
    }
}

public sealed record UpdateTemplateCommand(
    Guid ActorId,
    Guid TemplateId,
    string? Title,
    string? SubjectTemplate,
    string? BodyTemplate,
    string? Category,
    bool? IsActive) : IRequest<TemplateDto>;

public sealed class UpdateTemplateHandler(
    ILetterReader reader,
    ILetterJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<UpdateTemplateCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(UpdateTemplateCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, Permissions.LetterPermissions.TemplateManage, ct: cancellationToken);

        var template = await reader.FindTemplateAsync(command.TemplateId, cancellationToken)
            ?? throw new TemplateNotFoundException();

        template.Update(
            command.Title, command.SubjectTemplate, command.BodyTemplate,
            command.Category, command.IsActive, DateTime.UtcNow);
        await journal.SaveTemplateAsync(template, cancellationToken);
        return new(template.Id, template.Code, template.Title, template.CategoryCode,
            template.IsActive, template.CreatedOn, template.UpdatedOn);
    }
}

public sealed record DeactivateTemplateCommand(Guid ActorId, Guid TemplateId) : IRequest<Unit>;

public sealed class DeactivateTemplateHandler(
    ILetterReader reader,
    ILetterJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<DeactivateTemplateCommand, Unit>
{
    public async Task<Unit> Handle(DeactivateTemplateCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, Permissions.LetterPermissions.TemplateManage, ct: cancellationToken);

        var template = await reader.FindTemplateAsync(command.TemplateId, cancellationToken)
            ?? throw new TemplateNotFoundException();

        template.Update(null, null, null, null, isActive: false, DateTime.UtcNow);
        await journal.SaveTemplateAsync(template, cancellationToken);
        return Unit.Value;
    }
}
