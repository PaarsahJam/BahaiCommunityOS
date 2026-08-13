using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Knowledge.Application.DTOs;
using CommunityOS.Knowledge.Application.Logging;
using CommunityOS.Knowledge.Application.Permissions;
using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.Knowledge.Domain.Exceptions;
using CommunityOS.Knowledge.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Knowledge.Application.Commands;

public sealed record CreateWorkCommand(
    Guid ActorId,
    string Title,
    string WorkType,
    string OriginalLanguage,
    string DefaultLanguage) : IRequest<WorkDto>;

internal sealed class CreateWorkCommandHandler(
    IWorkRepository works,
    AuthorizationGuard guard,
    ILogger<CreateWorkCommandHandler> logger)
    : IRequestHandler<CreateWorkCommand, WorkDto>
{
    public async Task<WorkDto> Handle(CreateWorkCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.LibraryImport,
            new AuthorizationContext(ResourceType: "work"), ct);

        var work = Work.Create(
            cmd.Title,
            cmd.WorkType,
            cmd.OriginalLanguage,
            cmd.DefaultLanguage);

        await works.AddAsync(work, ct);
        logger.WorkCreated(work.Id, work.Title);

        return work.ToDto([]);
    }
}

public sealed record UpdateWorkCommand(
    Guid ActorId,
    Guid WorkId,
    string Title,
    string WorkType,
    string OriginalLanguage,
    string DefaultLanguage) : IRequest<WorkDto>;

internal sealed class UpdateWorkCommandHandler(
    IWorkRepository works,
    AuthorizationGuard guard)
    : IRequestHandler<UpdateWorkCommand, WorkDto>
{
    public async Task<WorkDto> Handle(UpdateWorkCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.LibraryUpdate,
            new AuthorizationContext(ResourceType: "work", ResourceId: cmd.WorkId), ct);

        var work = await works.GetByIdAsync(cmd.WorkId, ct)
            ?? throw new WorkNotFoundException(cmd.WorkId);

        work.UpdateDetails(cmd.Title, cmd.WorkType, cmd.OriginalLanguage, cmd.DefaultLanguage);
        await works.UpdateAsync(work, ct);

        return work.ToDto([]);
    }
}

public sealed record ImportEditionCommand(
    Guid ActorId,
    Guid WorkId,
    string Language,
    string? Translator,
    string? Publisher,
    int? EditionYear,
    bool Verified) : IRequest<EditionDto>;

internal sealed class ImportEditionCommandHandler(
    IEditionRepository editions,
    IWorkRepository works,
    AuthorizationGuard guard)
    : IRequestHandler<ImportEditionCommand, EditionDto>
{
    public async Task<EditionDto> Handle(ImportEditionCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.LibraryImport,
            new AuthorizationContext(ResourceType: "edition", ResourceId: cmd.WorkId), ct);

        if (!await works.ExistsAsync(cmd.WorkId, ct))
            throw new WorkNotFoundException(cmd.WorkId);

        var edition = Edition.Import(
            cmd.WorkId,
            cmd.Language,
            cmd.Translator,
            cmd.Publisher,
            cmd.EditionYear,
            cmd.Verified);

        await editions.AddAsync(edition, ct);
        return edition.ToDto();
    }
}

public sealed record VerifyEditionCommand(Guid ActorId, Guid EditionId) : IRequest<EditionDto>;

internal sealed class VerifyEditionCommandHandler(
    IEditionRepository editions,
    AuthorizationGuard guard)
    : IRequestHandler<VerifyEditionCommand, EditionDto>
{
    public async Task<EditionDto> Handle(VerifyEditionCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.LibraryVerify,
            new AuthorizationContext(ResourceType: "edition", ResourceId: cmd.EditionId), ct);

        var edition = await editions.GetByIdAsync(cmd.EditionId, ct)
            ?? throw new EditionNotFoundException(cmd.EditionId);

        edition.Verify();
        await editions.UpdateAsync(edition, ct);

        return edition.ToDto();
    }
}

public sealed record ImportPassageCommand(
    Guid ActorId,
    Guid EditionId,
    string ReferencePath,
    string Text,
    int SortOrder) : IRequest<PassageDto>;

internal sealed class ImportPassageCommandHandler(
    IPassageRepository passages,
    IEditionRepository editions,
    AuthorizationGuard guard)
    : IRequestHandler<ImportPassageCommand, PassageDto>
{
    public async Task<PassageDto> Handle(ImportPassageCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.LibraryImport,
            new AuthorizationContext(ResourceType: "passage", ResourceId: cmd.EditionId), ct);

        if (!await editions.ExistsAsync(cmd.EditionId, ct))
            throw new EditionNotFoundException(cmd.EditionId);

        var passage = Passage.Import(cmd.EditionId, cmd.ReferencePath, cmd.Text, cmd.SortOrder);
        await passages.AddAsync(passage, ct);

        return passage.ToDto();
    }
}

public sealed record CorrectPassageCommand(
    Guid ActorId,
    Guid PassageId,
    string Text) : IRequest<PassageDto>;

internal sealed class CorrectPassageCommandHandler(
    IPassageRepository passages,
    AuthorizationGuard guard)
    : IRequestHandler<CorrectPassageCommand, PassageDto>
{
    public async Task<PassageDto> Handle(CorrectPassageCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.LibraryImport,
            new AuthorizationContext(ResourceType: "passage", ResourceId: cmd.PassageId), ct);

        var passage = await passages.GetByIdAsync(cmd.PassageId, ct)
            ?? throw new PassageNotFoundException(cmd.PassageId);

        passage.Correct(cmd.Text);
        await passages.UpdateAsync(passage, ct);

        return passage.ToDto();
    }
}