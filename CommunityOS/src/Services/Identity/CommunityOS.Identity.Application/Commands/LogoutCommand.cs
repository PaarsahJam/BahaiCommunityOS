using CommunityOS.Identity.Application.Crypto;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record LogoutCommand(string RefreshToken) : IRequest;

internal sealed class LogoutCommandHandler(
    ISessionRepository sessions) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand cmd, CancellationToken ct)
    {
        var tokenHash = TokenHasher.Hash(cmd.RefreshToken);
        var session = await sessions.GetByRefreshTokenHashAsync(tokenHash, ct);
        if (session is null) return;

        session.Revoke("User logout.");
        await sessions.UpdateAsync(session, ct);
    }
}
