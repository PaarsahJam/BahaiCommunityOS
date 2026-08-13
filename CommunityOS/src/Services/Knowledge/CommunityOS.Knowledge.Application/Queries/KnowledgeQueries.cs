using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Knowledge.Application.DTOs;
using CommunityOS.Knowledge.Application.Permissions;
using CommunityOS.Knowledge.Domain.Exceptions;
using CommunityOS.Knowledge.Domain.Repositories;
using MediatR;

namespace CommunityOS.Knowledge.Application.Queries;

public sealed record ListQuestionsQuery(
    Guid ActorId,
    string? Status,
    Guid? CategoryId,
    Guid? OrganizationUnitId,
    string? Query) : IRequest<IReadOnlyList<QuestionDto>>;

internal sealed class ListQuestionsQueryHandler(
    IQuestionRepository questions,
    AuthorizationGuard guard)
    : IRequestHandler<ListQuestionsQuery, IReadOnlyList<QuestionDto>>
{
    public async Task<IReadOnlyList<QuestionDto>> Handle(ListQuestionsQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, KnowledgePermissions.QuestionRead,
            query.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "question")
                : new AuthorizationContext(ResourceType: "question"), ct);

        var result = query.OrganizationUnitId is { } filterUnit
            ? await questions.ListByOrganizationUnitAsync(filterUnit, ct)
            : await questions.ListAsync(ct);

        return result
            .Where(q => query.Status is null || q.Status.Name == query.Status)
            .Where(q => query.CategoryId is null || q.CategoryId == query.CategoryId)
            .Where(q => query.Query is null
                || q.Title.Contains(query.Query, StringComparison.OrdinalIgnoreCase)
                || q.Body.Contains(query.Query, StringComparison.OrdinalIgnoreCase))
            .Select(q => q.ToDto())
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetQuestionByIdQuery(Guid ActorId, Guid QuestionId) : IRequest<QuestionDto>;

internal sealed class GetQuestionByIdQueryHandler(
    IQuestionRepository questions,
    AuthorizationGuard guard)
    : IRequestHandler<GetQuestionByIdQuery, QuestionDto>
{
    public async Task<QuestionDto> Handle(GetQuestionByIdQuery query, CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(query.QuestionId, ct)
            ?? throw new QuestionNotFoundException(query.QuestionId);

        await guard.RequireAsync(query.ActorId, KnowledgePermissions.QuestionRead,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "question", question.Id)
                : new AuthorizationContext(ResourceType: "question", ResourceId: question.Id), ct);

        return question.ToDto();
    }
}

public sealed record ListAnswersByQuestionQuery(Guid ActorId, Guid QuestionId) : IRequest<IReadOnlyList<AnswerDto>>;

internal sealed class ListAnswersByQuestionQueryHandler(
    IAnswerRepository answers,
    IQuestionRepository questions,
    AuthorizationGuard guard)
    : IRequestHandler<ListAnswersByQuestionQuery, IReadOnlyList<AnswerDto>>
{
    public async Task<IReadOnlyList<AnswerDto>> Handle(ListAnswersByQuestionQuery query, CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(query.QuestionId, ct)
            ?? throw new QuestionNotFoundException(query.QuestionId);

        await guard.RequireAsync(query.ActorId, KnowledgePermissions.AnswerRead,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "answer", question.Id)
                : new AuthorizationContext(ResourceType: "answer", ResourceId: question.Id), ct);

        var result = await answers.ListByQuestionAsync(question.Id, ct);
        return result.Select(a => a.ToDto()).ToList().AsReadOnly();
    }
}

public sealed record GetAnswerByIdQuery(Guid ActorId, Guid AnswerId) : IRequest<AnswerDto>;

internal sealed class GetAnswerByIdQueryHandler(
    IAnswerRepository answers,
    IQuestionRepository questions,
    AuthorizationGuard guard)
    : IRequestHandler<GetAnswerByIdQuery, AnswerDto>
{
    public async Task<AnswerDto> Handle(GetAnswerByIdQuery query, CancellationToken ct)
    {
        var answer = await answers.GetByIdAsync(query.AnswerId, ct)
            ?? throw new AnswerNotFoundException(query.AnswerId);

        var question = await questions.GetByIdAsync(answer.QuestionId, ct)
            ?? throw new QuestionNotFoundException(answer.QuestionId);

        await guard.RequireAsync(query.ActorId, KnowledgePermissions.AnswerRead,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "answer", answer.Id)
                : new AuthorizationContext(ResourceType: "answer", ResourceId: answer.Id), ct);

        return answer.ToDto();
    }
}

public sealed record ListDiscussionsByQuestionQuery(Guid ActorId, Guid QuestionId) : IRequest<IReadOnlyList<DiscussionDto>>;

internal sealed class ListDiscussionsByQuestionQueryHandler(
    IDiscussionRepository discussions,
    IQuestionRepository questions,
    AuthorizationGuard guard)
    : IRequestHandler<ListDiscussionsByQuestionQuery, IReadOnlyList<DiscussionDto>>
{
    public async Task<IReadOnlyList<DiscussionDto>> Handle(ListDiscussionsByQuestionQuery query, CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(query.QuestionId, ct)
            ?? throw new QuestionNotFoundException(query.QuestionId);

        await guard.RequireAsync(query.ActorId, KnowledgePermissions.DiscussionRead,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "discussion", question.Id)
                : new AuthorizationContext(ResourceType: "discussion", ResourceId: question.Id), ct);

        var result = await discussions.ListByQuestionAsync(question.Id, ct);
        return result.Select(d => d.ToDto()).ToList().AsReadOnly();
    }
}

public sealed record ListAiSuggestionsByQuestionQuery(Guid ActorId, Guid QuestionId) : IRequest<IReadOnlyList<AiSuggestionDto>>;

internal sealed class ListAiSuggestionsByQuestionQueryHandler(
    IAiSuggestionRepository suggestions,
    IQuestionRepository questions,
    AuthorizationGuard guard)
    : IRequestHandler<ListAiSuggestionsByQuestionQuery, IReadOnlyList<AiSuggestionDto>>
{
    public async Task<IReadOnlyList<AiSuggestionDto>> Handle(ListAiSuggestionsByQuestionQuery query, CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(query.QuestionId, ct)
            ?? throw new QuestionNotFoundException(query.QuestionId);

        await guard.RequireAsync(query.ActorId, KnowledgePermissions.AiReview,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "ai", question.Id)
                : new AuthorizationContext(ResourceType: "ai", ResourceId: question.Id), ct);

        var result = await suggestions.ListByQuestionAsync(question.Id, ct);
        return result.Select(s => s.ToDto()).ToList().AsReadOnly();
    }
}

public sealed record ResolveCitationQuery(Guid ActorId, Guid PassageId) : IRequest<CitationDto>;

internal sealed class ResolveCitationQueryHandler(
    IPassageRepository passages,
    AuthorizationGuard guard)
    : IRequestHandler<ResolveCitationQuery, CitationDto>
{
    public async Task<CitationDto> Handle(ResolveCitationQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, KnowledgePermissions.LibraryRead,
            new AuthorizationContext(ResourceType: "passage", ResourceId: query.PassageId), ct);

        var passage = await passages.GetByIdAsync(query.PassageId, ct)
            ?? throw new PassageNotFoundException(query.PassageId);

        return passage.ToCitationDto();
    }
}