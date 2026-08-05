using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record AssignRoleCommand(Guid MemberId, string RoleName) : IRequest;

internal sealed class AssignRoleCommandHandler(IMemberRepository members)
    : IRequestHandler<AssignRoleCommand>
{
    public async Task Handle(AssignRoleCommand cmd, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(cmd.MemberId, ct)
            ?? throw new MemberNotFoundException(cmd.MemberId);

        var role = Role.Create(cmd.RoleName);
        member.AssignRole(role);
        await members.UpdateAsync(member, ct);
    }
}
