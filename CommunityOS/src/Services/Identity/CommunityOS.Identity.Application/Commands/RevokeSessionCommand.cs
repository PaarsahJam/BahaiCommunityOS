using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

/// <summary>
/// Revokes a single session belonging to the authenticated user account.
/// The actor identity comes exclusively from the JWT subject; the session is
/// located by id and revoked only when it belongs to that actor.
/// </summary>
public sealed record RevokeSessionCommand(
    Guid UserAccountId,
    Guid SessionId) : IRequest;

internal sealed class RevokeSessionCommandHandler(
    ISessionRepository sessions) : IRequestHandler<RevokeSessionCommand>
{
    public async Task Handle(RevokeSessionCommand cmd, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(cmd.SessionId, ct)
            ?? throw new SessionNotFoundException(cmd.SessionId);

        if (session.UserAccountId != cmd.UserAccountId)
            throw new SessionNotFoundException(cmd.SessionId);

        session.Revoke("User revoked session.");
        await sessions.UpdateAsync(session, ct);
    }
}