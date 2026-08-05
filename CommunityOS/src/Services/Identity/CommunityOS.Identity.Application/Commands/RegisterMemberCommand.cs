using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record RegisterMemberCommand(
    string FirstName,
    string LastName,
    string? MiddleName,
    string Email,
    string? PhoneNumber,
    Guid? LocalUnitId) : IRequest<MemberDto>;

internal sealed class RegisterMemberCommandHandler(IMemberRepository members)
    : IRequestHandler<RegisterMemberCommand, MemberDto>
{
    public async Task<MemberDto> Handle(RegisterMemberCommand cmd, CancellationToken ct)
    {
        var email = Email.Create(cmd.Email);

        if (await members.GetByEmailAsync(email, ct) is not null)
            throw new DuplicateEmailException(email.Value);

        var name = PersonName.Create(cmd.FirstName, cmd.LastName, cmd.MiddleName);
        var member = Member.Create(name, email, cmd.LocalUnitId);

        await members.AddAsync(member, ct);

        return member.ToDto();
    }
}
