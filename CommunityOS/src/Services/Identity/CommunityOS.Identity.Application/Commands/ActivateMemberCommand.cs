using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record ActivateMemberCommand(Guid MemberId) : IRequest;

internal sealed class ActivateMemberCommandHandler(IMemberRepository members)
    : IRequestHandler<ActivateMemberCommand>
{
    public async Task Handle(ActivateMemberCommand cmd, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(cmd.MemberId, ct)
            ?? throw new MemberNotFoundException(cmd.MemberId);

        member.Activate();
        await members.UpdateAsync(member, ct);
    }
}
