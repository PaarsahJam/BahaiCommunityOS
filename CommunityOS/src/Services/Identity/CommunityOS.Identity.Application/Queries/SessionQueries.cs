using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Queries;

/// <summary>
/// Lists the account's own active-session rows. <paramref name="SessionFamilyId"/>
/// is the logical session id read from the authenticated token's signed <c>sid</c>
/// claim and is used solely to mark the current session; a null value (legacy or
/// malformed claim) yields <c>IsCurrent = false</c> for every row.
/// </summary>
public sealed record ListSessionsQuery(
    Guid UserAccountId,
    Guid? SessionFamilyId = null) : IRequest<IReadOnlyList<SessionDto>>;

internal sealed class ListSessionsQueryHandler(
    ISessionRepository sessions,
    IUserAccountRepository userAccounts) : IRequestHandler<ListSessionsQuery, IReadOnlyList<SessionDto>>
{
    public async Task<IReadOnlyList<SessionDto>> Handle(ListSessionsQuery query, CancellationToken ct)
    {
        // The account aggregate (with its owned devices) is loaded both to
        // enforce existence and to resolve each session's device metadata.
        // Owned entity types are always included when the owner is queried, so
        // no separate device query is issued.
        var account = await userAccounts.GetByIdAsync(query.UserAccountId, ct)
            ?? throw new UserAccountNotFoundException(query.UserAccountId);

        var deviceById = account.Devices.ToDictionary(d => d.Id);

        var result = await sessions.GetActiveByUserAsync(query.UserAccountId, ct);
        return result.Select(s =>
            {
                var device = deviceById.GetValueOrDefault(s.DeviceId);
                return new SessionDto(
                    s.Id,
                    s.DeviceId,
                    device?.Name,
                    device?.Platform,
                    s.CreatedOn,
                    s.ExpiresOn,
                    s.LastUsedOn,
                    s.IsActive,
                    query.SessionFamilyId.HasValue &&
                        s.TokenFamilyId == query.SessionFamilyId.Value);
            })
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
