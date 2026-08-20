using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Repositories;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using CommunityOS.Workflow.Application.Pipeline;
using static CommunityOS.Workflow.Infrastructure.Integration.WorkflowReconciliation;

namespace CommunityOS.Workflow.Infrastructure.Integration.Knowledge;

/// <summary>
/// Reconciles <c>knowledge-moderation</c> and <c>knowledge-ai-review</c> tasks
/// from Knowledge integration events (ADR-024). The outcome is advisory:
/// accepting/rejecting an AI suggestion or publishing a question is a
/// Knowledge-side governance action performed through the Knowledge API, never
/// by Workflow. Reconciliation is create-if-absent / close-if-open and
/// idempotent under duplicates.
/// </summary>
public sealed class KnowledgeReviewIntegrationEventConsumer(
    IWorkflowTaskRepository tasks,
    IMediator mediator,
    ILogger<KnowledgeReviewIntegrationEventConsumer> logger) :
    IConsumer<Contracts.Knowledge.QuestionFlagged>,
    IConsumer<Contracts.Knowledge.QuestionUnderReview>,
    IConsumer<Contracts.Knowledge.QuestionMerged>,
    IConsumer<Contracts.Knowledge.QuestionArchived>,
    IConsumer<Contracts.Knowledge.AiSuggestionRequested>,
    IConsumer<Contracts.Knowledge.AiSuggestionReviewed>
{
    public Task Consume(ConsumeContext<Contracts.Knowledge.QuestionFlagged> context) =>
        CreateModerationTaskIfAbsentAsync(context.Message.QuestionId, context.Message.OccurredOn, context.CancellationToken);

    public Task Consume(ConsumeContext<Contracts.Knowledge.QuestionUnderReview> context) =>
        CreateModerationTaskIfAbsentAsync(context.Message.QuestionId, context.Message.OccurredOn, context.CancellationToken);

    public async Task Consume(ConsumeContext<Contracts.Knowledge.QuestionMerged> context)
    {
        var message = context.Message;
        await CloseModerationAsync(message.QuestionId, "merged", message.OccurredOn, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<Contracts.Knowledge.QuestionArchived> context)
    {
        var message = context.Message;
        await CloseModerationAsync(message.QuestionId, "archived", message.OccurredOn, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<Contracts.Knowledge.AiSuggestionRequested> context)
    {
        var message = context.Message;
        return CreateAiReviewTaskIfAbsentAsync(message.SuggestionId, message.QuestionId, message.OccurredOn, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<Contracts.Knowledge.AiSuggestionReviewed> context)
    {
        var message = context.Message;
        await CompleteAiReviewAsync(message.SuggestionId, message.Outcome, message.OccurredOn, context.CancellationToken);
    }

    private async Task CreateModerationTaskIfAbsentAsync(Guid questionId, DateTime occurredOn, CancellationToken ct)
    {
        var task = await CreateIfAbsentAsync(
            tasks, KnowledgeModerationDefinitionCode, KnowledgeQuestionDomainType, questionId,
            originatorId: SystemActorId, occurredOn, ct);
        if (task is not null)
            logger.ModerationTaskCreated(task.Id, questionId);
    }

    private async Task CloseModerationAsync(Guid questionId, string outcome, DateTime occurredOn, CancellationToken ct)
    {
        var task = await CloseIfOpenAsync(
            tasks, mediator, KnowledgeModerationDefinitionCode, KnowledgeQuestionDomainType, questionId,
            outcome, SystemActorId, occurredOn, ct);
        if (task is not null)
            logger.ModerationTaskCompleted(task.Id, questionId, outcome);
    }

    private async Task CreateAiReviewTaskIfAbsentAsync(
        Guid suggestionId, Guid questionId, DateTime occurredOn, CancellationToken ct)
    {
        var task = await CreateIfAbsentAsync(
            tasks, KnowledgeAiReviewDefinitionCode, KnowledgeAiSuggestionDomainType, suggestionId,
            originatorId: SystemActorId, occurredOn, ct);
        if (task is not null)
            logger.AiReviewTaskCreated(task.Id, suggestionId, questionId);
    }

    private async Task CompleteAiReviewAsync(Guid suggestionId, string outcome, DateTime occurredOn, CancellationToken ct)
    {
        var task = await CloseIfOpenAsync(
            tasks, mediator, KnowledgeAiReviewDefinitionCode, KnowledgeAiSuggestionDomainType, suggestionId,
            outcome, SystemActorId, occurredOn, ct);
        if (task is not null)
            logger.AiReviewTaskCompleted(task.Id, suggestionId, outcome);
    }
}

internal static partial class KnowledgeReviewIntegrationEventConsumerLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Created knowledge-moderation task {TaskId} for question {QuestionId}.")]
    public static partial void ModerationTaskCreated(this ILogger logger, Guid taskId, Guid questionId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Completed knowledge-moderation task {TaskId} for question {QuestionId} with outcome {Outcome}.")]
    public static partial void ModerationTaskCompleted(this ILogger logger, Guid taskId, Guid questionId, string outcome);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Created knowledge-ai-review task {TaskId} for suggestion {SuggestionId} (question {QuestionId}).")]
    public static partial void AiReviewTaskCreated(this ILogger logger, Guid taskId, Guid suggestionId, Guid questionId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Completed knowledge-ai-review task {TaskId} for suggestion {SuggestionId} with outcome {Outcome}.")]
    public static partial void AiReviewTaskCompleted(this ILogger logger, Guid taskId, Guid suggestionId, string outcome);
}