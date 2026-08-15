using CommunityOS.Contracts.Documents;
using CommunityOS.Documents.Application.Pipeline;
using MassTransit;
using MediatR;

namespace CommunityOS.Documents.Infrastructure.Integration;

/// <summary>
/// Forwards the application-layer download notification (raised only for
/// protected/sensitive content that was actually downloaded) onto the message
/// bus as the ratified <c>DocumentContentDownloaded</c> integration event
/// (ADR-022). No binary content or secrets are ever exported. A future Audit
/// service may consume this; Documents has no direct Audit dependency.
/// </summary>
public sealed class DocumentContentDownloadNotificationHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DocumentContentDownloadNotification>
{
    public async Task Handle(DocumentContentDownloadNotification notification, CancellationToken cancellationToken)
    {
        await publishEndpoint.Publish(
            new DocumentContentDownloaded(
                notification.DocumentId,
                notification.VersionId,
                notification.ActorId,
                DateTime.UtcNow),
            cancellationToken);
    }
}