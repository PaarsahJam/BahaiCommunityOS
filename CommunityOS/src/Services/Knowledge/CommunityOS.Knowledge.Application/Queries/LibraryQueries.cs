using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Knowledge.Application.DTOs;
using CommunityOS.Knowledge.Application.Permissions;
using CommunityOS.Knowledge.Domain.Exceptions;
using CommunityOS.Knowledge.Domain.Repositories;
using MediatR;

namespace CommunityOS.Knowledge.Application.Queries;

public sealed record ListWorksQuery(Guid ActorId, Guid? CategoryId, Guid? TopicId) : IRequest<IReadOnlyList<WorkDto>>;

internal sealed class ListWorksQueryHandler(
    IWorkRepository works,
    AuthorizationGuard guard)
    : IRequestHandler<ListWorksQuery, IReadOnlyList<WorkDto>>
{
    public async Task<IReadOnlyList<WorkDto>> Handle(ListWorksQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, KnowledgePermissions.LibraryRead,
            new AuthorizationContext(ResourceType: "work"), ct);

        var result = await works.ListAsync(ct);
        return result.Select(w => w.ToDto([])).ToList().AsReadOnly();
    }
}

public sealed record GetWorkByIdQuery(Guid ActorId, Guid WorkId) : IRequest<WorkDto>;

internal sealed class GetWorkByIdQueryHandler(
    IWorkRepository works,
    IEditionRepository editions,
    AuthorizationGuard guard)
    : IRequestHandler<GetWorkByIdQuery, WorkDto>
{
    public async Task<WorkDto> Handle(GetWorkByIdQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, KnowledgePermissions.LibraryRead,
            new AuthorizationContext(ResourceType: "work", ResourceId: query.WorkId), ct);

        var work = await works.GetByIdAsync(query.WorkId, ct)
            ?? throw new WorkNotFoundException(query.WorkId);

        var workEditions = await editions.ListByWorkAsync(work.Id, ct);
        return work.ToDto(workEditions);
    }
}

public sealed record ListEditionsQuery(
    Guid ActorId,
    Guid? WorkId,
    string? Language,
    bool? Verified) : IRequest<IReadOnlyList<EditionDto>>;

internal sealed class ListEditionsQueryHandler(
    IEditionRepository editions,
    AuthorizationGuard guard)
    : IRequestHandler<ListEditionsQuery, IReadOnlyList<EditionDto>>
{
    public async Task<IReadOnlyList<EditionDto>> Handle(ListEditionsQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, KnowledgePermissions.LibraryRead,
            new AuthorizationContext(ResourceType: "edition"), ct);

        var result = query.WorkId is { } workId
            ? await editions.ListByWorkAsync(workId, ct)
            : throw new ArgumentException("A workId filter is required to list editions.");

        return result
            .Where(e => query.Language is null || e.Language == query.Language)
            .Where(e => query.Verified is null || e.Verified == query.Verified)
            .Select(e => e.ToDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetEditionByIdQuery(Guid ActorId, Guid EditionId) : IRequest<EditionDto>;

internal sealed class GetEditionByIdQueryHandler(
    IEditionRepository editions,
    AuthorizationGuard guard)
    : IRequestHandler<GetEditionByIdQuery, EditionDto>
{
    public async Task<EditionDto> Handle(GetEditionByIdQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, KnowledgePermissions.LibraryRead,
            new AuthorizationContext(ResourceType: "edition", ResourceId: query.EditionId), ct);

        var edition = await editions.GetByIdAsync(query.EditionId, ct)
            ?? throw new EditionNotFoundException(query.EditionId);

        return edition.ToDto();
    }
}

public sealed record ListPassagesQuery(
    Guid ActorId,
    Guid? EditionId,
    int? From,
    int? To) : IRequest<IReadOnlyList<PassageDto>>;

internal sealed class ListPassagesQueryHandler(
    IPassageRepository passages,
    AuthorizationGuard guard)
    : IRequestHandler<ListPassagesQuery, IReadOnlyList<PassageDto>>
{
    public async Task<IReadOnlyList<PassageDto>> Handle(ListPassagesQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, KnowledgePermissions.LibraryRead,
            new AuthorizationContext(ResourceType: "passage"), ct);

        if (query.EditionId is not { } editionId)
            throw new ArgumentException("An editionId filter is required to list passages.");

        var result = await passages.ListByEditionAsync(editionId, ct);

        return result
            .Where(p => query.From is null || p.SortOrder >= query.From)
            .Where(p => query.To is null || p.SortOrder <= query.To)
            .Select(p => p.ToDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetPassageByIdQuery(Guid ActorId, Guid PassageId) : IRequest<PassageDto>;

internal sealed class GetPassageByIdQueryHandler(
    IPassageRepository passages,
    AuthorizationGuard guard)
    : IRequestHandler<GetPassageByIdQuery, PassageDto>
{
    public async Task<PassageDto> Handle(GetPassageByIdQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, KnowledgePermissions.LibraryRead,
            new AuthorizationContext(ResourceType: "passage", ResourceId: query.PassageId), ct);

        var passage = await passages.GetByIdAsync(query.PassageId, ct)
            ?? throw new PassageNotFoundException(query.PassageId);

        return passage.ToDto();
    }
}

public sealed record ListCategoriesQuery(Guid ActorId) : IRequest<IReadOnlyList<CategoryDto>>;

internal sealed class ListCategoriesQueryHandler(
    ICategoryRepository categories,
    AuthorizationGuard guard)
    : IRequestHandler<ListCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<IReadOnlyList<CategoryDto>> Handle(ListCategoriesQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, KnowledgePermissions.LibraryRead,
            new AuthorizationContext(ResourceType: "category"), ct);

        var result = await categories.ListAsync(ct);
        return result.Select(c => c.ToDto()).ToList().AsReadOnly();
    }
}

public sealed record ListTopicsQuery(Guid ActorId) : IRequest<IReadOnlyList<TopicDto>>;

internal sealed class ListTopicsQueryHandler(
    ITopicRepository topics,
    AuthorizationGuard guard)
    : IRequestHandler<ListTopicsQuery, IReadOnlyList<TopicDto>>
{
    public async Task<IReadOnlyList<TopicDto>> Handle(ListTopicsQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, KnowledgePermissions.LibraryRead,
            new AuthorizationContext(ResourceType: "topic"), ct);

        var result = await topics.ListAsync(ct);
        return result.Select(t => t.ToDto()).ToList().AsReadOnly();
    }
}