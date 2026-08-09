using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Queries;

public sealed record ListSessionsQuery(Guid UserAccountId) : IRequest<IReadOnlyList<SessionDto>>;

internal sealed class ListSessionsQueryHandler(
    ISessionRepository sessions,
    IUserAccountRepository userAccounts) : IRequestHandler<ListSessionsQuery, IReadOnlyList<SessionDto>>
{
    public async Task<IReadOnlyList<SessionDto>> Handle(ListSessionsQuery query, CancellationToken ct)
    {
        _ = await userAccounts.GetByIdAsync(query.UserAccountId, ct)
            ?? throw new UserAccountNotFoundException(query.UserAccountId);

        var result = await sessions.GetActiveByUserAsync(query.UserAccountId, ct);
        return result.Select(s => new SessionDto(
                s.Id, s.DeviceId, s.CreatedOn, s.ExpiresOn, s.LastUsedOn, s.IsActive))
            .ToList().AsReadOnly();
    }
}

public sealed record ListSecurityEventsQuery(Guid UserAccountId, int Take = 100)
    : IRequest<IReadOnlyList<SecurityEventDto>>;

internal sealed class ListSecurityEventsQueryHandler(
    ISecurityEventRepository securityEvents,
    IUserAccountRepository userAccounts) : IRequestHandler<ListSecurityEventsQuery, IReadOnlyList<SecurityEventDto>>
{
    public async Task<IReadOnlyList<SecurityEventDto>> Handle(
        ListSecurityEventsQuery query, CancellationToken ct)
    {
        _ = await userAccounts.GetByIdAsync(query.UserAccountId, ct)
            ?? throw new UserAccountNotFoundException(query.UserAccountId);

        var result = await securityEvents.GetByUserAsync(query.UserAccountId, query.Take, ct);
        return result.Select(e => new SecurityEventDto(
                e.Id, e.EventType, e.Description, e.OccurredOn))
            .ToList().AsReadOnly();
    }
}
