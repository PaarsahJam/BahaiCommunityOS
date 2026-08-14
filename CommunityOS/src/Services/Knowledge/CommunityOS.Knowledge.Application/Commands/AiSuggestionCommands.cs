using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Knowledge.Application.DTOs;
using CommunityOS.Knowledge.Application.Permissions;
using DomainEvents = CommunityOS.Knowledge.Application.Pipeline.DomainEventPublisher;
using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.Knowledge.Domain.Exceptions;
using CommunityOS.Knowledge.Domain.Repositories;
using MediatR;

namespace CommunityOS.Knowledge.Application.Commands;

public sealed record RequestAiSuggestionCommand(
    Guid ActorId,
    Guid QuestionId,
    string ModelId,
    string Instruction,
    IReadOnlyList<Guid>? PassageIds) : IRequest<AiSuggestionDto>;

internal sealed class RequestAiSuggestionCommandHandler(
    IAiSuggestionRepository suggestions,
    IQuestionRepository questions,
    IPassageRepository passages,
    IEditionRepository editions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<RequestAiSuggestionCommand, AiSuggestionDto>
{
    public async Task<AiSuggestionDto> Handle(RequestAiSuggestionCommand cmd, CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(cmd.QuestionId, ct)
            ?? throw new QuestionNotFoundException(cmd.QuestionId);

        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.AiSuggest,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "ai", cmd.QuestionId)
                : new AuthorizationContext(ResourceType: "ai", ResourceId: cmd.QuestionId), ct);

        // The suggestion body is drafted from the cited passages only; it is a
        // non-authoritative draft that always requires a human moderator review
        // before it can become an answer.
        var body = DraftFromPassages(cmd.Instruction, cmd.PassageIds ?? []);
        var suggestion = AiSuggestion.Request(
            cmd.QuestionId,
            question.OrganizationUnitId,
            body,
            cmd.ModelId,
            promptVersion: "1");

        foreach (var passageId in cmd.PassageIds ?? [])
        {
            var passage = await passages.GetByIdAsync(passageId, ct)
                ?? throw new InvalidReferenceException(passageId);

            var edition = await editions.GetByIdAsync(passage.EditionId, ct)
                ?? throw new InvalidReferenceException(passageId);

            if (!edition.Verified)
                throw new InvalidReferenceException(passageId);
        }

        await suggestions.AddAsync(suggestion, ct);
        await DomainEvents.PublishAsync(suggestion, mediator, ct);
        return suggestion.ToDto();
    }

    private static string DraftFromPassages(string instruction, IReadOnlyList<Guid> passageIds)
    {
        var ids = passageIds.Count == 0
            ? "no passages"
            : string.Join(", ", passageIds);
        return $"AI draft — instruction: {instruction} — cites: {ids}.";
    }
}

public sealed record AcceptAiSuggestionCommand(
    Guid ActorId,
    Guid SuggestionId) : IRequest<AiSuggestionDto>;

internal sealed class AcceptAiSuggestionCommandHandler(
    IAiSuggestionRepository suggestions,
    IAnswerRepository answers,
    IQuestionRepository questions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<AcceptAiSuggestionCommand, AiSuggestionDto>
{
    public async Task<AiSuggestionDto> Handle(AcceptAiSuggestionCommand cmd, CancellationToken ct)
    {
        var suggestion = await suggestions.GetByIdAsync(cmd.SuggestionId, ct)
            ?? throw new AiSuggestionNotFoundException(cmd.SuggestionId);

        var question = await questions.GetByIdAsync(suggestion.QuestionId, ct)
            ?? throw new QuestionNotFoundException(suggestion.QuestionId);

        // Acceptance is a human moderator grant; the AI itself can never accept
        // its own output.
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.AiReview,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "ai", cmd.SuggestionId)
                : new AuthorizationContext(ResourceType: "ai", ResourceId: cmd.SuggestionId), ct);

        if (suggestion.ReviewState != AiSuggestionReviewState.Suggested)
            throw new AiSuggestionAlreadyReviewedException(cmd.SuggestionId);

        suggestion.Review(AiSuggestionReviewState.Accepted);

        // Acceptance materializes an AI-sourced answer carrying the original
        // model id; it still follows the normal question lifecycle.
        var answer = Answer.CreateFromAiSuggestion(
            question.Id,
            cmd.ActorId,
            suggestion.Body,
            suggestion.ModelId,
            suggestion.Id);

        await answers.AddAsync(answer, ct);
        await suggestions.UpdateAsync(suggestion, ct);

        // The accepted suggestion materializes a normal authoritative Answer
        // (source = "ai") that follows the same lifecycle and emits the same
        // AnswerAdded integration event as the member-authored path, so
        // consumers (search indexing, notifications, read models) observe it.
        // Acceptance was the explicit human-governed step; the AI never
        // publishes an answer by itself.
        await DomainEvents.PublishAsync(answer, mediator, ct);
        await DomainEvents.PublishAsync(suggestion, mediator, ct);

        return suggestion.ToDto();
    }
}

public sealed record RejectAiSuggestionCommand(
    Guid ActorId,
    Guid SuggestionId) : IRequest<AiSuggestionDto>;

internal sealed class RejectAiSuggestionCommandHandler(
    IAiSuggestionRepository suggestions,
    IQuestionRepository questions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<RejectAiSuggestionCommand, AiSuggestionDto>
{
    public async Task<AiSuggestionDto> Handle(RejectAiSuggestionCommand cmd, CancellationToken ct)
    {
        var suggestion = await suggestions.GetByIdAsync(cmd.SuggestionId, ct)
            ?? throw new AiSuggestionNotFoundException(cmd.SuggestionId);

        var question = await questions.GetByIdAsync(suggestion.QuestionId, ct)
            ?? throw new QuestionNotFoundException(suggestion.QuestionId);

        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.AiReview,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "ai", cmd.SuggestionId)
                : new AuthorizationContext(ResourceType: "ai", ResourceId: cmd.SuggestionId), ct);

        suggestion.Review(AiSuggestionReviewState.Rejected);
        await suggestions.UpdateAsync(suggestion, ct);
        await DomainEvents.PublishAsync(suggestion, mediator, ct);

        return suggestion.ToDto();
    }
}