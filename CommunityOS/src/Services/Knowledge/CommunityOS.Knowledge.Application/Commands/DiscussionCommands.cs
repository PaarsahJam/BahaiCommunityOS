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

public sealed record CreateDiscussionCommand(
    Guid ActorId,
    Guid QuestionId,
    string Title) : IRequest<DiscussionDto>;

internal sealed class CreateDiscussionCommandHandler(
    IDiscussionRepository discussions,
    IQuestionRepository questions,
    AuthorizationGuard guard)
    : IRequestHandler<CreateDiscussionCommand, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(CreateDiscussionCommand cmd, CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(cmd.QuestionId, ct)
            ?? throw new QuestionNotFoundException(cmd.QuestionId);

        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.DiscussionCreate,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "discussion", cmd.QuestionId)
                : new AuthorizationContext(ResourceType: "discussion", ResourceId: cmd.QuestionId), ct);

        var discussion = Discussion.Create(
            cmd.QuestionId,
            question.OrganizationUnitId,
            cmd.Title,
            cmd.ActorId);

        await discussions.AddAsync(discussion, ct);
        return discussion.ToDto();
    }
}

public sealed record AddCommentCommand(
    Guid ActorId,
    Guid DiscussionId,
    string Body,
    IReadOnlyList<Guid>? PassageIds) : IRequest<DiscussionDto>;

internal sealed class AddCommentCommandHandler(
    IDiscussionRepository discussions,
    IQuestionRepository questions,
    IReferenceRepository references,
    IPassageRepository passages,
    IEditionRepository editions,
    AuthorizationGuard guard)
    : IRequestHandler<AddCommentCommand, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        var discussion = await discussions.GetByIdAsync(cmd.DiscussionId, ct)
            ?? throw new DiscussionNotFoundException(cmd.DiscussionId);

        var question = await questions.GetByIdAsync(discussion.QuestionId, ct)
            ?? throw new QuestionNotFoundException(discussion.QuestionId);

        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.DiscussionCreate,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "discussion", cmd.DiscussionId)
                : new AuthorizationContext(ResourceType: "discussion", ResourceId: cmd.DiscussionId), ct);

        var comment = discussion.AddComment(cmd.ActorId, cmd.Body);

        foreach (var passageId in cmd.PassageIds ?? [])
        {
            var passage = await passages.GetByIdAsync(passageId, ct)
                ?? throw new InvalidReferenceException(passageId);

            var edition = await editions.GetByIdAsync(passage.EditionId, ct)
                ?? throw new InvalidReferenceException(passageId);

            if (!edition.Verified)
                throw new InvalidReferenceException(passageId);

            var reference = Reference.Create(ReferenceOwnerType.Comment, comment.Id, passageId, edition.Id);
            await references.AddAsync(reference, ct);
        }

        await discussions.UpdateAsync(discussion, ct);
        return discussion.ToDto();
    }
}

public sealed record ModerateDiscussionCommand(
    Guid ActorId,
    Guid DiscussionId,
    string Action) : IRequest<DiscussionDto>;

internal sealed class ModerateDiscussionCommandHandler(
    IDiscussionRepository discussions,
    IQuestionRepository questions,
    AuthorizationGuard guard)
    : IRequestHandler<ModerateDiscussionCommand, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(ModerateDiscussionCommand cmd, CancellationToken ct)
    {
        var discussion = await discussions.GetByIdAsync(cmd.DiscussionId, ct)
            ?? throw new DiscussionNotFoundException(cmd.DiscussionId);

        var question = await questions.GetByIdAsync(discussion.QuestionId, ct)
            ?? throw new QuestionNotFoundException(discussion.QuestionId);

        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.DiscussionModerate,
            question.OrganizationUnitId is { } unitId
                ? new AuthorizationContext(unitId, "discussion", cmd.DiscussionId)
                : new AuthorizationContext(ResourceType: "discussion", ResourceId: cmd.DiscussionId), ct);

        switch (cmd.Action.ToLowerInvariant())
        {
            case "hide":
                discussion.Hide();
                break;
            case "delete":
                discussion.Delete();
                break;
            default:
                throw new ArgumentException($"Unknown moderation action '{cmd.Action}'.");
        }

        await discussions.UpdateAsync(discussion, ct);
        return discussion.ToDto();
    }
}