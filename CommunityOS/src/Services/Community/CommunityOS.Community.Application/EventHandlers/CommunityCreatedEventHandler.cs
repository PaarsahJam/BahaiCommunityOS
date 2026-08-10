using CommunityOS.Community.Application.Logging;
using CommunityOS.Community.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Community.Application.EventHandlers;

internal sealed class CommunityCreatedEventHandler(ILogger<CommunityCreatedEventHandler> logger)
    : INotificationHandler<CommunityCreatedEvent>
{
    public Task Handle(CommunityCreatedEvent notification, CancellationToken ct)
    {
        logger.CommunityCreated(notification.CommunityId, notification.Name);

        return Task.CompletedTask;
    }
}
