using CommunityOS.Identity.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Identity.Application.EventHandlers;

internal sealed class MemberCreatedEventHandler(ILogger<MemberCreatedEventHandler> logger)
    : INotificationHandler<MemberCreatedEvent>
{
    public Task Handle(MemberCreatedEvent notification, CancellationToken ct)
    {
        logger.LogInformation(
            "Member created: {MemberId} ({Email})",
            notification.MemberId,
            notification.Email);

        return Task.CompletedTask;
    }
}
