using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Queries;

public sealed record GetCurrentUserQuery(Guid UserAccountId) : IRequest<UserAccountDto>;

internal sealed class GetCurrentUserQueryHandler(
    IUserAccountRepository userAccounts) : IRequestHandler<GetCurrentUserQuery, UserAccountDto>
{
    public async Task<UserAccountDto> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        var account = await userAccounts.GetByIdAsync(query.UserAccountId, ct)
            ?? throw new UserAccountNotFoundException(query.UserAccountId);

        return new UserAccountDto(
            account.Id,
            account.Email.Value,
            account.Status.Name,
            account.CreatedOn,
            account.VerifiedOn,
            account.LastLoginOn,
            account.ExternalIdentities
                .Where(x => x.IsActive)
                .Select(x => new ExternalIdentityDto(x.Id, x.Provider, x.Subject, x.LinkedOn))
                .ToList().AsReadOnly(),
            account.MfaMethods
                .Select(m => new MfaMethodDto(
                    m.Id, m.Type.Name, m.IsVerified, m.IsActive, m.CreatedOn))
                .ToList().AsReadOnly(),
            account.Devices
                .Select(d => new DeviceDto(
                    d.Id, d.Name, d.Platform, d.RegisteredOn, d.IsCurrentlyTrusted))
                .ToList().AsReadOnly());
    }
}
