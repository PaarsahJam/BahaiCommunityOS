using CommunityOS.Contracts.Knowledge;
using CommunityOS.Knowledge.Domain.Events;
using CommunityOS.SharedKernel.Domain.Events;
using MassTransit;
using MediatR;

namespace CommunityOS.Knowledge.Infrastructure.Integration;

/// <summary>
/// Publishes Knowledge domain events onto the message bus as integration
/// events so other services can react. Only stable ids and lifecycle state are
/// placed on the bus — no author PII is ever exported; consumers resolve
/// authors through the Community API. AI output is exported only as a
/// suggestion review record, never as authoritative content.
/// </summary>
public sealed class KnowledgeIntegrationEventPublisher(IPublishEndpoint publishEndpoint)
    : INotificationHandler<IDomainEvent>
{
    public async Task Handle(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case WorkImportedEvent e:
                await publishEndpoint.Publish(
                    new WorkImported(e.WorkId, e.Title, e.WorkType, e.OccurredOn), cancellationToken);
                break;
            case EditionImportedEvent e:
                await publishEndpoint.Publish(
                    new EditionImported(e.EditionId, e.WorkId, e.Language, e.Verified, e.OccurredOn), cancellationToken);
                break;
            case EditionVerifiedEvent e:
                await publishEndpoint.Publish(
                    new EditionVerified(e.EditionId, e.WorkId, e.OccurredOn), cancellationToken);
                break;
            case PassageImportedEvent e:
                await publishEndpoint.Publish(
                    new PassageImported(e.PassageId, e.EditionId, e.ReferencePath, e.OccurredOn), cancellationToken);
                break;
            case PassageCorrectedEvent e:
                await publishEndpoint.Publish(
                    new PassageCorrected(e.PassageId, e.EditionId, e.Revision, e.OccurredOn), cancellationToken);
                break;
            case QuestionSubmittedEvent e:
                await publishEndpoint.Publish(
                    new QuestionSubmitted(e.QuestionId, e.AuthorId, e.OrganizationUnitId, e.OccurredOn), cancellationToken);
                break;
            case QuestionPublishedEvent e:
                await publishEndpoint.Publish(
                    new QuestionPublished(e.QuestionId, e.OrganizationUnitId, e.OccurredOn), cancellationToken);
                break;
            case QuestionFlaggedEvent e:
                await publishEndpoint.Publish(
                    new QuestionFlagged(e.QuestionId, e.FlagReason, e.OccurredOn), cancellationToken);
                break;
            case QuestionUnderReviewEvent e:
                await publishEndpoint.Publish(
                    new QuestionUnderReview(e.QuestionId, e.OccurredOn), cancellationToken);
                break;
            case QuestionMergedEvent e:
                await publishEndpoint.Publish(
                    new QuestionMerged(e.QuestionId, e.TargetQuestionId, e.OccurredOn), cancellationToken);
                break;
            case QuestionArchivedEvent e:
                await publishEndpoint.Publish(
                    new QuestionArchived(e.QuestionId, e.OccurredOn), cancellationToken);
                break;
            case AnswerAddedEvent e:
                await publishEndpoint.Publish(
                    new AnswerAdded(e.AnswerId, e.QuestionId, e.AuthorId, e.Source, e.OccurredOn), cancellationToken);
                break;
            case AnswerUpdatedEvent e:
                await publishEndpoint.Publish(
                    new AnswerUpdated(e.AnswerId, e.QuestionId, e.Revision, e.OccurredOn), cancellationToken);
                break;
            case AnswerAcceptedEvent e:
                await publishEndpoint.Publish(
                    new AnswerAccepted(e.AnswerId, e.QuestionId, e.OccurredOn), cancellationToken);
                break;
            case AiSuggestionRequestedEvent e:
                await publishEndpoint.Publish(
                    new AiSuggestionRequested(e.SuggestionId, e.QuestionId, e.ModelId, e.OccurredOn), cancellationToken);
                break;
            case AiSuggestionReviewedEvent e:
                await publishEndpoint.Publish(
                    new AiSuggestionReviewed(e.SuggestionId, e.QuestionId, e.Outcome, e.OccurredOn), cancellationToken);
                break;
            case CategoryCreatedEvent e:
                await publishEndpoint.Publish(
                    new CategoryCreated(e.CategoryId, e.Name, e.OccurredOn), cancellationToken);
                break;
            case CategoryUpdatedEvent e:
                await publishEndpoint.Publish(
                    new CategoryUpdated(e.CategoryId, e.Name, e.OccurredOn), cancellationToken);
                break;
            // Topic and Work-update domain events are deliberately not exported.
            case WorkUpdatedEvent:
            case TopicCreatedEvent:
            case TopicUpdatedEvent:
                break;
        }
    }
}