using CommunityOS.Identity.Application.Authentication;
using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.Crypto;
using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Entities;
using CommunityOS.Identity.Domain.Enumerations;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommunityOS.Identity.Tests.Application;

/// <summary>
/// Verifies that every access-token issuance path passes the issuing session's
/// logical token-family id (carried as the signed <c>sid</c> claim): login and
/// OAuth exchange create a fresh family, refresh preserves the existing family.
/// </summary>
public sealed class SessionIssuanceTests
{
    [Fact]
    public async Task Login_IssuesAccessToken_WithTheFreshlyCreatedFamily()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        account.Verify();

        var authenticator = Substitute.For<IAccountAuthenticator>();
        authenticator.AuthenticateAsync(Arg.Any<AuthenticateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new Authenticated(account));

        var tokenService = Substitute.For<ITokenService>();
        tokenService.GenerateRefreshToken().Returns("refresh-token-1");
        var issuedFamily = Guid.Empty;
        var issuedEpoch = long.MinValue;
        tokenService.GenerateAccessToken(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>())
            .Returns("access-token-1")
            .AndDoes(call =>
            {
                issuedFamily = call.ArgAt<Guid>(1);
                issuedEpoch = call.ArgAt<long>(3);
            });

        var sessions = Substitute.For<ISessionRepository>();
        Session? added = null;
        sessions.AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => added = call.Arg<Session>());

        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.UpdateAsync(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var securityEvents = Substitute.For<ISecurityEventRepository>();
        securityEvents.AddAsync(Arg.Any<SecurityEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var logger = NullLogger<LoginCommandHandler>.Instance;

        var handler = new LoginCommandHandler(
            userAccounts, sessions, securityEvents, authenticator, tokenService, logger);

        var result = await handler.Handle(
            new LoginCommand(account.Email.Value, "password", DeviceName: "Phone"), CancellationToken.None);

        result.Tokens.Should().NotBeNull();
        added.Should().NotBeNull();
        added!.SessionRevocationEpochAtIssue.Should().Be(account.SessionRevocationEpoch,
            "a newly issued refresh family binds to the account epoch observed at issuance");
        issuedFamily.Should().Be(added.TokenFamilyId);
        issuedFamily.Should().NotBe(Guid.Empty);
        issuedEpoch.Should().Be(account.SessionRevocationEpoch,
            "a newly issued access token must carry the account's current epoch");
    }

    [Fact]
    public async Task Refresh_IssuesAccessToken_PreservingTheExistingFamily()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        account.Verify();
        account.AdvanceSessionRevocationEpochOnce();
        var device = account.RegisterDevice("Phone", "Android", null);
        var session = Session.Create(
            account.Id, device.Id, TokenHasher.Hash("refresh-token-original"), TimeSpan.FromDays(30),
            sessionRevocationEpochAtIssue: account.SessionRevocationEpoch);

        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetByRefreshTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(session);
        sessions.ReloadAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        sessions.UpdateAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        Session? rotated = null;
        sessions.AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => rotated = call.Arg<Session>());

        var userAccounts = Substitute.For<IUserAccountRepository>();
        // The decision must use the account obtained under the row lock; the
        // plain pre-lock loader must never be consulted during a refresh.
        userAccounts.GetByIdForUpdateAsync(session.UserAccountId, Arg.Any<CancellationToken>())
            .Returns(account);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var tokenService = Substitute.For<ITokenService>();
        tokenService.GenerateRefreshToken().Returns("refresh-token-rotated");
        var issuedFamily = Guid.Empty;
        var issuedEpoch = long.MinValue;
        tokenService.GenerateAccessToken(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>())
            .Returns("access-token-1")
            .AndDoes(call =>
            {
                issuedFamily = call.ArgAt<Guid>(1);
                issuedEpoch = call.ArgAt<long>(3);
            });

        var securityEvents = Substitute.For<ISecurityEventRepository>();
        securityEvents.AddAsync(Arg.Any<SecurityEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var logger = NullLogger<RefreshSessionCommandHandler>.Instance;

        var handler = new RefreshSessionCommandHandler(
            sessions, userAccounts, tokenService, securityEvents, unitOfWork, logger);

        await handler.Handle(
            new RefreshSessionCommand("refresh-token-original"), CancellationToken.None);

        rotated.Should().NotBeNull();
        rotated!.TokenFamilyId.Should().Be(session.TokenFamilyId);
        rotated.SessionRevocationEpochAtIssue.Should().Be(account.SessionRevocationEpoch,
            "the rotated session binds to the epoch read under the account lock");
        issuedFamily.Should().Be(session.TokenFamilyId);
        issuedEpoch.Should().Be(account.SessionRevocationEpoch,
            "a refreshed access token must carry the account's current epoch");

        // The refresh performed its decision inside one explicit transaction:
        // it began before the account lock, committed exactly once, and the
        // rotation rows were flushed before that single commit.
        Received.InOrder(() =>
        {
            unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            userAccounts.GetByIdForUpdateAsync(session.UserAccountId, Arg.Any<CancellationToken>());
            sessions.ReloadAsync(session, Arg.Any<CancellationToken>());
            sessions.UpdateAsync(session, Arg.Any<CancellationToken>());
            sessions.AddAsync(rotated!, Arg.Any<CancellationToken>());
            unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });

        await userAccounts.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OAuthExchange_IssuesAccessToken_WithTheFreshlyCreatedFamily()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        account.Verify();
        var client = OAuthClient.Create(
            "client-1", "Test app", OAuthClientType.Public,
            ["https://app/redirect"], ["authorization_code"], ["openid"]);
        var code = AuthorizationCode.Create(
            account.Id, client.Id, client.ClientId, "https://app/redirect",
            "verifier", AuthorizationCode.ChallengeMethodPlain, "openid",
            TokenHasher.Hash("raw-code"), "nonce", TimeSpan.FromMinutes(10));

        var authorizationCodes = Substitute.For<IAuthorizationCodeRepository>();
        authorizationCodes.GetByCodeHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(code);
        authorizationCodes.UpdateAsync(Arg.Any<AuthorizationCode>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var oauthClients = Substitute.For<IOAuthClientRepository>();
        oauthClients.GetByClientIdAsync("client-1", Arg.Any<CancellationToken>()).Returns(client);

        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        userAccounts.UpdateAsync(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sessions = Substitute.For<ISessionRepository>();
        Session? added = null;
        sessions.AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => added = call.Arg<Session>());

        var securityEvents = Substitute.For<ISecurityEventRepository>();
        securityEvents.AddAsync(Arg.Any<SecurityEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tokenService = Substitute.For<ITokenService>();
        tokenService.GenerateRefreshToken().Returns("refresh-token-1");
        tokenService.GenerateIdToken(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<string>(), Arg.Any<string?>()).Returns("id-token-1");
        var issuedFamily = Guid.Empty;
        var issuedEpoch = long.MinValue;
        tokenService.GenerateAccessToken(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<long>())
            .Returns("access-token-1")
            .AndDoes(call =>
            {
                issuedFamily = call.ArgAt<Guid>(1);
                issuedEpoch = call.ArgAt<long>(3);
            });

        var mediator = Substitute.For<MediatR.IMediator>();

        var handler = new ExchangeAuthorizationCodeCommandHandler(
            authorizationCodes, oauthClients, userAccounts, sessions,
            securityEvents, tokenService, mediator);

        var result = await handler.Handle(
            new ExchangeAuthorizationCodeCommand("raw-code", "verifier", "client-1", "https://app/redirect"),
            CancellationToken.None);

        added.Should().NotBeNull();
        added!.SessionRevocationEpochAtIssue.Should().Be(account.SessionRevocationEpoch,
            "an OAuth-issued refresh family binds to the account epoch observed at issuance");
        issuedFamily.Should().Be(added.TokenFamilyId);
        issuedFamily.Should().NotBe(Guid.Empty);
        issuedEpoch.Should().Be(account.SessionRevocationEpoch,
            "an OAuth-issued access token must carry the account's current epoch");
        result.AccessToken.Should().Be("access-token-1");
        result.RefreshToken.Should().Be("refresh-token-1");
    }
}