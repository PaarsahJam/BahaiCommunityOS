using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Knowledge.Application.DTOs;
using CommunityOS.Knowledge.Application.Permissions;
using DomainEvents = CommunityOS.Knowledge.Application.Pipeline.DomainEventPublisher;
using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Exceptions;
using CommunityOS.Knowledge.Domain.Repositories;
using MediatR;

namespace CommunityOS.Knowledge.Application.Commands;

public sealed record CreateQuestionCommand(
    Guid ActorId,
    string Title,
    string Body,
    Guid? CategoryId,
    Guid? OrganizationUnitId,
    IReadOnlyList<string>? Tags) : IRequest<QuestionDto>;

internal sealed class CreateQuestionCommandHandler(
    IQuestionRepository questions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<CreateQuestionCommand, QuestionDto>
{
    public async Task<QuestionDto> Handle(CreateQuestionCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.QuestionCreate,
            cmd.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "question")
                : new AuthorizationContext(ResourceType: "question"), ct);

        var question = Question.Create(
            cmd.Title,
            cmd.Body,
            cmd.ActorId,
            cmd.CategoryId,
            cmd.OrganizationUnitId,
            cmd.Tags);

        await questions.AddAsync(question, ct);
        await DomainEvents.PublishAsync(question, mediator, ct);

        return question.ToDto();
    }
}

public sealed record SubmitQuestionCommand(Guid ActorId, Guid QuestionId) : IRequest<QuestionDto>;

internal sealed class SubmitQuestionCommandHandler(
    IQuestionRepository questions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<SubmitQuestionCommand, QuestionDto>
{
    public async Task<QuestionDto> Handle(SubmitQuestionCommand cmd, CancellationToken ct)
    {
        var question = await QuestionAccess.LoadForActorAsync(cmd.ActorId, cmd.QuestionId, questions, guard,
            KnowledgePermissions.QuestionUpdate, ct);

        question.Submit();
        await questions.UpdateAsync(question, ct);
        await DomainEvents.PublishAsync(question, mediator, ct);

        return question.ToDto();
    }
}

public sealed record PublishQuestionCommand(Guid ActorId, Guid QuestionId) : IRequest<QuestionDto>;

internal sealed class PublishQuestionCommandHandler(
    IQuestionRepository questions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<PublishQuestionCommand, QuestionDto>
{
    public async Task<QuestionDto> Handle(PublishQuestionCommand cmd, CancellationToken ct)
    {
        var question = await QuestionAccess.LoadForActorAsync(cmd.ActorId, cmd.QuestionId, questions, guard,
            KnowledgePermissions.ModerationReview, ct);

        question.Publish();
        await questions.UpdateAsync(question, ct);
        await DomainEvents.PublishAsync(question, mediator, ct);

        return question.ToDto();
    }
}

public sealed record FlagQuestionCommand(Guid ActorId, Guid QuestionId, string Reason) : IRequest<QuestionDto>;

internal sealed class FlagQuestionCommandHandler(
    IQuestionRepository questions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<FlagQuestionCommand, QuestionDto>
{
    public async Task<QuestionDto> Handle(FlagQuestionCommand cmd, CancellationToken ct)
    {
        var question = await QuestionAccess.LoadForActorAsync(cmd.ActorId, cmd.QuestionId, questions, guard,
            KnowledgePermissions.QuestionUpdate, ct);

        question.Flag(cmd.Reason);
        await questions.UpdateAsync(question, ct);
        await DomainEvents.PublishAsync(question, mediator, ct);

        return question.ToDto();
    }
}

public sealed record MoveUnderReviewCommand(Guid ActorId, Guid QuestionId) : IRequest<QuestionDto>;

internal sealed class MoveUnderReviewCommandHandler(
    IQuestionRepository questions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<MoveUnderReviewCommand, QuestionDto>
{
    public async Task<QuestionDto> Handle(MoveUnderReviewCommand cmd, CancellationToken ct)
    {
        var question = await QuestionAccess.LoadForActorAsync(cmd.ActorId, cmd.QuestionId, questions, guard,
            KnowledgePermissions.ModerationReview, ct);

        question.MoveUnderReview();
        await questions.UpdateAsync(question, ct);
        await DomainEvents.PublishAsync(question, mediator, ct);

        return question.ToDto();
    }
}

public sealed record MergeQuestionCommand(
    Guid ActorId,
    Guid QuestionId,
    Guid TargetQuestionId) : IRequest<QuestionDto>;

internal sealed class MergeQuestionCommandHandler(
    IQuestionRepository questions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<MergeQuestionCommand, QuestionDto>
{
    public async Task<QuestionDto> Handle(MergeQuestionCommand cmd, CancellationToken ct)
    {
        var question = await QuestionAccess.LoadForActorAsync(cmd.ActorId, cmd.QuestionId, questions, guard,
            KnowledgePermissions.QuestionMerge, ct);

        var target = await questions.GetByIdAsync(cmd.TargetQuestionId, ct)
            ?? throw new QuestionNotFoundException(cmd.TargetQuestionId);

        question.MergeOnto(cmd.TargetQuestionId);
        target.Canonicalize();

        await questions.UpdateAsync(question, ct);
        await questions.UpdateAsync(target, ct);
        await DomainEvents.PublishAsync(question, mediator, ct);
        await DomainEvents.PublishAsync(target, mediator, ct);

        return question.ToDto();
    }
}

public sealed record ArchiveQuestionCommand(Guid ActorId, Guid QuestionId) : IRequest<QuestionDto>;

internal sealed class ArchiveQuestionCommandHandler(
    IQuestionRepository questions,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<ArchiveQuestionCommand, QuestionDto>
{
    public async Task<QuestionDto> Handle(ArchiveQuestionCommand cmd, CancellationToken ct)
    {
        var question = await QuestionAccess.LoadForActorAsync(cmd.ActorId, cmd.QuestionId, questions, guard,
            KnowledgePermissions.ModerationArchive, ct);

        question.Archive();
        await questions.UpdateAsync(question, ct);
        await DomainEvents.PublishAsync(question, mediator, ct);

        return question.ToDto();
    }
}

internal static class QuestionAccess
{
    internal static async Task<Question> LoadForActorAsync(
        Guid actorId,
        Guid questionId,
        IQuestionRepository questions,
        AuthorizationGuard guard,
        string permission,
        CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(questionId, ct)
            ?? throw new QuestionNotFoundException(questionId);

        await guard.RequireAsync(actorId, permission,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "question", questionId)
                : new AuthorizationContext(ResourceType: "question", ResourceId: questionId), ct);

        return question;
    }
}