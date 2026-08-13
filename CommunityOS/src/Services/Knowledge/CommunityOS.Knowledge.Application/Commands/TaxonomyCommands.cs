using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Knowledge.Application.DTOs;
using CommunityOS.Knowledge.Application.Permissions;
using DomainEvents = CommunityOS.Knowledge.Application.Pipeline.DomainEventPublisher;
using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Exceptions;
using CommunityOS.Knowledge.Domain.Repositories;
using MediatR;

namespace CommunityOS.Knowledge.Application.Commands;

public sealed record CreateCategoryCommand(
    Guid ActorId,
    string Name,
    string? Description) : IRequest<CategoryDto>;

internal sealed class CreateCategoryCommandHandler(
    ICategoryRepository categories,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    public async Task<CategoryDto> Handle(CreateCategoryCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.ModerationManage,
            new AuthorizationContext(ResourceType: "category"), ct);

        var category = Category.Create(cmd.Name, cmd.Description);
        await categories.AddAsync(category, ct);
        await DomainEvents.PublishAsync(category, mediator, ct);

        return category.ToDto();
    }
}

public sealed record UpdateCategoryCommand(
    Guid ActorId,
    Guid CategoryId,
    string Name,
    string? Description) : IRequest<CategoryDto>;

internal sealed class UpdateCategoryCommandHandler(
    ICategoryRepository categories,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    public async Task<CategoryDto> Handle(UpdateCategoryCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.ModerationManage,
            new AuthorizationContext(ResourceType: "category", ResourceId: cmd.CategoryId), ct);

        var category = await categories.GetByIdAsync(cmd.CategoryId, ct)
            ?? throw new CategoryNotFoundException(cmd.CategoryId);

        category.Update(cmd.Name, cmd.Description);
        await categories.UpdateAsync(category, ct);
        await DomainEvents.PublishAsync(category, mediator, ct);

        return category.ToDto();
    }
}

public sealed record CreateTopicCommand(
    Guid ActorId,
    string Name,
    string? Description) : IRequest<TopicDto>;

internal sealed class CreateTopicCommandHandler(
    ITopicRepository topics,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<CreateTopicCommand, TopicDto>
{
    public async Task<TopicDto> Handle(CreateTopicCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.ModerationManage,
            new AuthorizationContext(ResourceType: "topic"), ct);

        var topic = Topic.Create(cmd.Name, cmd.Description);
        await topics.AddAsync(topic, ct);
        await DomainEvents.PublishAsync(topic, mediator, ct);

        return topic.ToDto();
    }
}

public sealed record UpdateTopicCommand(
    Guid ActorId,
    Guid TopicId,
    string Name,
    string? Description) : IRequest<TopicDto>;

internal sealed class UpdateTopicCommandHandler(
    ITopicRepository topics,
    AuthorizationGuard guard,
    IMediator mediator)
    : IRequestHandler<UpdateTopicCommand, TopicDto>
{
    public async Task<TopicDto> Handle(UpdateTopicCommand cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.ActorId, KnowledgePermissions.ModerationManage,
            new AuthorizationContext(ResourceType: "topic", ResourceId: cmd.TopicId), ct);

        var topic = await topics.GetByIdAsync(cmd.TopicId, ct)
            ?? throw new TopicNotFoundException(cmd.TopicId);

        topic.Update(cmd.Name, cmd.Description);
        await topics.UpdateAsync(topic, ct);
        await DomainEvents.PublishAsync(topic, mediator, ct);

        return topic.ToDto();
    }
}