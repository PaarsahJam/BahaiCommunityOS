using CommunityOS.Notifications.Application.DTOs;
using CommunityOS.Notifications.Domain.Repositories;
using MediatR;

namespace CommunityOS.Notifications.Application.Queries;

/// <summary>
/// Recipient-scoped member inbox/list contract (ADR-027): the authenticated
/// actor's own notifications, newest-first, bounded server-side. The
/// authorization basis is the persisted recipient relationship — never an
/// organization scope, a role, or a client-supplied recipient identity. The
/// recipient is always the authenticated actor; no <c>memberId</c> route or
/// query parameter is accepted anywhere on this surface.
/// </summary>
public sealed record ListMemberNotificationsQuery(Guid ActorId, int Limit, int Offset)
    : IRequest<IReadOnlyList<MemberNotificationSummaryDto>>
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 50;
}

internal sealed class ListMemberNotificationsQueryHandler(INotificationRepository notifications)
    : IRequestHandler<ListMemberNotificationsQuery, IReadOnlyList<MemberNotificationSummaryDto>>
{
    public async Task<IReadOnlyList<MemberNotificationSummaryDto>> Handle(
        ListMemberNotificationsQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, ListMemberNotificationsQuery.MaxPageSize);
        var offset = Math.Max(query.Offset, 0);

        // Over-fetch of limit+1 lets callers detect a next page without a
        // separate count query; the final result is capped to limit. The
        // repository filters recipients and excludes sensitive rows at the
        // query boundary (fail-closed; no recipient distribution materialized).
        var candidates = await notifications.ListMemberInboxAsync(
            query.ActorId, limit + 1, offset, ct);

        return candidates
            .Take(limit)
            .Select(n => n.ToMemberSummaryDto(query.ActorId))
            .ToList()
            .AsReadOnly();
    }
}

/// <summary>
/// Recipient-scoped unread count for the authenticated actor (delivered, not
/// read, not sensitive). Never reveals which notifications are unread.
/// </summary>
public sealed record GetMemberUnreadCountQuery(Guid ActorId)
    : IRequest<MemberUnreadCountDto>;

internal sealed class GetMemberUnreadCountQueryHandler(INotificationRepository notifications)
    : IRequestHandler<GetMemberUnreadCountQuery, MemberUnreadCountDto>
{
    public async Task<MemberUnreadCountDto> Handle(
        GetMemberUnreadCountQuery query, CancellationToken ct)
    {
        var count = await notifications.CountUnreadByMemberAsync(query.ActorId, ct);
        return new MemberUnreadCountDto(count);
    }
}