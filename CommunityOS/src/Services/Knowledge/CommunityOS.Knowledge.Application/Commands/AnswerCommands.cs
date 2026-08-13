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

public sealed record CreateAnswerCommand(
    Guid ActorId,
    Guid QuestionId,
    string Body,
    IReadOnlyList<Guid>? PassageIds) : IRequest<AnswerDto>;

internal sealed class CreateAnswerCommandHandler(
    IAnswerRepository answers,
    IQuestionRepository questions,
    IReferenceRepository references,
    IPassageRepository passages,
    IEditionRepository editions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<CreateAnswerCommand, AnswerDto>
{
    public async Task<AnswerDto> Handle(CreateAnswerCommand cmd, CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(cmd.QuestionId, ct)
            ?? throw new QuestionNotFoundException(cmd.QuestionId);

        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.AnswerCreate,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "answer", cmd.QuestionId)
                : new AuthorizationContext(ResourceType: "answer", ResourceId: cmd.QuestionId), ct);

        var answer = Answer.Create(cmd.QuestionId, cmd.ActorId, cmd.Body);

        foreach (var passageId in cmd.PassageIds ?? [])
        {
            var passage = await passages.GetByIdAsync(passageId, ct)
                ?? throw new InvalidReferenceException(passageId);

            var edition = await editions.GetByIdAsync(passage.EditionId, ct)
                ?? throw new InvalidReferenceException(passageId);

            if (!edition.Verified)
                throw new InvalidReferenceException(passageId);

            var reference = Reference.Create(ReferenceOwnerType.Answer, answer.Id, passageId, edition.Id);
            await references.AddAsync(reference, ct);
        }

        await answers.AddAsync(answer, ct);
        await DomainEvents.PublishAsync(answer, mediator, ct);

        return answer.ToDto();
    }
}

public sealed record UpdateAnswerCommand(Guid ActorId, Guid AnswerId, string Body) : IRequest<AnswerDto>;

internal sealed class UpdateAnswerCommandHandler(
    IAnswerRepository answers,
    IQuestionRepository questions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<UpdateAnswerCommand, AnswerDto>
{
    public async Task<AnswerDto> Handle(UpdateAnswerCommand cmd, CancellationToken ct)
    {
        var answer = await answers.GetByIdAsync(cmd.AnswerId, ct)
            ?? throw new AnswerNotFoundException(cmd.AnswerId);

        var question = await questions.GetByIdAsync(answer.QuestionId, ct)
            ?? throw new QuestionNotFoundException(answer.QuestionId);

        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.AnswerUpdate,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "answer", cmd.AnswerId)
                : new AuthorizationContext(ResourceType: "answer", ResourceId: cmd.AnswerId), ct);

        answer.Update(cmd.Body);
        await answers.UpdateAsync(answer, ct);
        await DomainEvents.PublishAsync(answer, mediator, ct);

        return answer.ToDto();
    }
}

public sealed record AcceptAnswerCommand(Guid ActorId, Guid QuestionId, Guid AnswerId) : IRequest<QuestionDto>;

internal sealed class AcceptAnswerCommandHandler(
    IAnswerRepository answers,
    IQuestionRepository questions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<AcceptAnswerCommand, QuestionDto>
{
    public async Task<QuestionDto> Handle(AcceptAnswerCommand cmd, CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(cmd.QuestionId, ct)
            ?? throw new QuestionNotFoundException(cmd.QuestionId);

        var answer = await answers.GetByIdAsync(cmd.AnswerId, ct)
            ?? throw new AnswerNotFoundException(cmd.AnswerId);

        if (answer.QuestionId != cmd.QuestionId)
            throw new AnswerNotOnQuestionException(cmd.AnswerId, cmd.QuestionId);

        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.ModerationReview,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "answer", cmd.QuestionId)
                : new AuthorizationContext(ResourceType: "answer", ResourceId: cmd.QuestionId), ct);

        question.AcceptAnswer(cmd.AnswerId);
        answer.MarkAccepted();

        await questions.UpdateAsync(question, ct);
        await answers.UpdateAsync(answer, ct);
        await DomainEvents.PublishAsync(question, mediator, ct);

        return question.ToDto();
    }
}