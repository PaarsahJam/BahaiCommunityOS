using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record SuspendMemberCommand(Guid MemberId, string Reason) : IRequest;

internal sealed class SuspendMemberCommandHandler(IMemberRepository members)
    : IRequestHandler<SuspendMemberCommand>
{
    public async Task Handle(SuspendMemberCommand cmd, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(cmd.MemberId, ct)
            ?? throw new MemberNotFoundException(cmd.MemberId);

        member.Suspend(cmd.Reason);
        await members.UpdateAsync(member, ct);
    }
}
