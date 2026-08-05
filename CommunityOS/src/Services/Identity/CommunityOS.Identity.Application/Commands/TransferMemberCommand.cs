using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record TransferMemberCommand(Guid MemberId, Guid TargetLocalUnitId) : IRequest;

internal sealed class TransferMemberCommandHandler(IMemberRepository members)
    : IRequestHandler<TransferMemberCommand>
{
    public async Task Handle(TransferMemberCommand cmd, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(cmd.MemberId, ct)
            ?? throw new MemberNotFoundException(cmd.MemberId);

        member.Transfer(cmd.TargetLocalUnitId);
        await members.UpdateAsync(member, ct);
    }
}
