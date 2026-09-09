using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.Crypto;
using FluentValidation;

namespace CommunityOS.Identity.Application.Validators;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.MfaCode).Length(6).When(x => x.MfaCode is not null);
        RuleFor(x => x.DeviceName).MaximumLength(100).When(x => x.DeviceName is not null);
    }
}

public sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}

public sealed class CompleteMfaEnrollmentCommandValidator : AbstractValidator<CompleteMfaEnrollmentCommand>
{
    public CompleteMfaEnrollmentCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotEmpty();
        RuleFor(x => x.MfaMethodId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}

public sealed class RemoveMfaCommandValidator : AbstractValidator<RemoveMfaCommand>
{
    public RemoveMfaCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotEmpty();
        RuleFor(x => x.MfaMethodId).NotEmpty();
    }
}

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
    }
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotEmpty();
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}

public sealed class RevokeSessionCommandValidator : AbstractValidator<RevokeSessionCommand>
{
    public RevokeSessionCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty();
    }
}

public sealed class LinkExternalIdentityCommandValidator : AbstractValidator<LinkExternalIdentityCommand>
{
    public LinkExternalIdentityCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotEmpty();
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(256);
    }
}

public sealed class CreateAuthorizationCodeCommandValidator : AbstractValidator<CreateAuthorizationCodeCommand>
{
    public CreateAuthorizationCodeCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RedirectUri).NotEmpty().MaximumLength(2048)
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage("The redirect URI must be an absolute URI.");
        RuleFor(x => x.CodeChallenge).NotEmpty().MaximumLength(256);
        RuleFor(x => x.CodeChallengeMethod).NotEmpty()
            .Must(Pkce.IsSupportedMethod)
            .WithMessage("code_challenge_method must be 'S256' or 'plain'.");
        RuleFor(x => x.Scope).NotEmpty().MaximumLength(500);
        RuleFor(x => x.MfaCode).Length(6).When(x => x.MfaCode is not null);
        RuleFor(x => x.State).MaximumLength(500).When(x => x.State is not null);
        RuleFor(x => x.Nonce).MaximumLength(500).When(x => x.Nonce is not null);
    }
}

public sealed class ExchangeAuthorizationCodeCommandValidator : AbstractValidator<ExchangeAuthorizationCodeCommand>
{
    public ExchangeAuthorizationCodeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RedirectUri).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.CodeVerifier).NotEmpty()
            .Length(43, 128)
            .Must(Pkce.IsValidVerifierFormat)
            .WithMessage("code_verifier must contain only unreserved characters.");
    }
}
