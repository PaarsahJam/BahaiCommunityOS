using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record UpdateMemberContactCommand(
    Guid MemberId,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateProvince,
    string? PostalCode,
    string? CountryCode) : IRequest;

internal sealed class UpdateMemberContactCommandHandler(IMemberRepository members)
    : IRequestHandler<UpdateMemberContactCommand>
{
    public async Task Handle(UpdateMemberContactCommand cmd, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(cmd.MemberId, ct)
            ?? throw new MemberNotFoundException(cmd.MemberId);

        var phone = cmd.PhoneNumber is not null
            ? PhoneNumber.Create(cmd.PhoneNumber)
            : null;

        var address = cmd.AddressLine1 is not null && cmd.City is not null
                   && cmd.PostalCode is not null && cmd.CountryCode is not null
            ? Address.Create(cmd.AddressLine1, cmd.City, cmd.PostalCode, cmd.CountryCode,
                cmd.AddressLine2, cmd.StateProvince)
            : null;

        member.UpdateContact(phone, address);
        await members.UpdateAsync(member, ct);
    }
}
