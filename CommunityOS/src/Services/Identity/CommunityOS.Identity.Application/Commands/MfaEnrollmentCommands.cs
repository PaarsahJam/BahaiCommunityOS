using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using MediatR;

namespace CommunityOS.Identity.Application.Commands;

public sealed record BeginMfaEnrollmentCommand(Guid UserAccountId) : IRequest<MfaEnrollmentDto>;

internal sealed class BeginMfaEnrollmentCommandHandler(
    IUserAccountRepository userAccounts,
    ITotpService totpService) : IRequestHandler<BeginMfaEnrollmentCommand, MfaEnrollmentDto>
{
    public async Task<MfaEnrollmentDto> Handle(BeginMfaEnrollmentCommand cmd, CancellationToken ct)
    {
        var account = await userAccounts.GetByIdAsync(cmd.UserAccountId, ct)
            ?? throw new UserAccountNotFoundException(cmd.UserAccountId);

        var secret = totpService.GenerateSecret();
        var method = account.EnrollMfa(MfaMethodType.AuthenticatorApp, secret);
        await userAccounts.UpdateAsync(account, ct);

        var provisioningUri = BuildProvisioningUri(account.Email.Value, secret);
        return new MfaEnrollmentDto(method.Id, secret, provisioningUri);
    }

    private static string BuildProvisioningUri(string email, string secret)
    {
        var issuer = Uri.EscapeDataString("CommunityOS");
        var label = Uri.EscapeDataString(email);
        return $"otpauth://totp/{label}?issuer={issuer}&secret={secret}&algorithm=SHA1&digits=6&period=30";
    }
}

public sealed record CompleteMfaEnrollmentCommand(Guid MfaMethodId, string Code) : IRequest;

internal sealed class CompleteMfaEnrollmentCommandHandler(
    IUserAccountRepository userAccounts,
    ITotpService totpService) : IRequestHandler<CompleteMfaEnrollmentCommand>
{
    public async Task Handle(CompleteMfaEnrollmentCommand cmd, CancellationToken ct)
    {
        var account = await userAccounts.GetByMfaMethodIdAsync(cmd.MfaMethodId, ct)
            ?? throw new UserAccountNotFoundException(Guid.Empty);

        var method = account.MfaMethods.FirstOrDefault(m => m.Id == cmd.MfaMethodId)
            ?? throw new InvalidMfaCodeException();

        if (!totpService.Verify(method.Secret, cmd.Code))
            throw new InvalidMfaCodeException();

        account.VerifyMfa(cmd.MfaMethodId);
        await userAccounts.UpdateAsync(account, ct);
    }
}
