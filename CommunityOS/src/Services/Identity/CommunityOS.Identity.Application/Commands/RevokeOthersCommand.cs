using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

/// <summary>
/// Revokes every non-revoked session row belonging to the authenticated user
/// account's other logical session families, preserving the current family.
/// The actor identity and the current family come exclusively from the JWT
/// subject and the signed <c>sid</c> claim; neither is accepted from the client.
/// </summary>
public sealed record RevokeOthersCommand(
    Guid UserAccountId,
    Guid CurrentTokenFamilyId) : IRequest;

internal sealed class RevokeOthersCommandHandler(
    ISessionRepository sessions) : IRequestHandler<RevokeOthersCommand>
{
    public Task Handle(RevokeOthersCommand cmd, CancellationToken ct) =>
        sessions.RevokeAllExceptFamilyForUserAsync(
            cmd.UserAccountId, cmd.CurrentTokenFamilyId, "User revoked other sessions.", ct);
}