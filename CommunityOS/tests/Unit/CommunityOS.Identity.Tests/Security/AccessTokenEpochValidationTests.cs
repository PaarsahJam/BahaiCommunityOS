using CommunityOS.Identity.API.Security;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.Identity.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CommunityOS.Identity.Tests.Security;

/// <summary>
/// ADR-036 D1/Q1: the Identity JWT validation seam. Tokens are minted exactly
/// as the production <see cref="JwtTokenService"/> mints them (or crafted with
/// hostile payloads), validated through a real <see cref="JwtSecurityTokenHandler"/>,
/// and then run through <see cref="AccessTokenEpochValidationEvents"/> exactly as
/// the <c>JwtBearer</c> handler wires it. The locally known epoch comes from
/// <see cref="IUserAccountRepository"/>.
/// </summary>
public sealed class AccessTokenEpochValidationTests
{
    private static readonly Guid AccountId = Guid.NewGuid();

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();

    private static RsaSigningKeyProvider Provider() => new(EmptyConfig());

    private static TokenValidationParameters Parameters(RsaSigningKeyProvider provider) =>
        new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "CommunityOS.Identity",
            ValidAudience = "CommunityOS",
            IssuerSigningKey = provider.SecurityKey
        };

    private static ClaimsPrincipal Validate(RsaSigningKeyProvider provider, string jwt) =>
        new JwtSecurityTokenHandler().ValidateToken(jwt, Parameters(provider), out _);

    private static string IssueAccessToken(RsaSigningKeyProvider provider, Guid accountId, long epoch) =>
        new JwtTokenService(provider, EmptyConfig())
            .GenerateAccessToken(accountId, Guid.NewGuid(), "user@example.com", epoch);

    private static string Sign(RsaSigningKeyProvider provider, Action<JwtPayload> configure)
    {
        var credentials = new SigningCredentials(provider.SecurityKey, SecurityAlgorithms.RsaSha256);
        var payload = new JwtPayload(
            "CommunityOS.Identity", "CommunityOS", null, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15));
        configure(payload);
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(new JwtHeader(credentials), payload));
    }

    private static IUserAccountRepository RepositoryFor(UserAccount? account)
    {
        var repository = Substitute.For<IUserAccountRepository>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(account));
        return repository;
    }

    private static UserAccount AccountAtEpoch(int advances)
    {
        var account = UserAccount.Register(
            Email.Create($"epoch-{Guid.NewGuid():N}@example.org"), "password-hash");
        for (var i = 0; i < advances; i++)
            account.AdvanceSessionRevocationEpochOnce();
        return account;
    }

    private static async Task<TokenValidatedContext> RunSeamAsync(ClaimsPrincipal principal, UserAccount? account)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => RepositoryFor(account));
        var serviceProvider = services.BuildServiceProvider();

        var context = new TokenValidatedContext(
            new DefaultHttpContext { RequestServices = serviceProvider },
            new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme, displayName: null, handlerType: typeof(JwtBearerHandler)),
            new JwtBearerOptions())
        {
            Principal = principal
        };

        await AccessTokenEpochValidationEvents.OnTokenValidatedAsync(context);
        return context;
    }

    [Fact]
    public async Task TokenEpoch_EqualToAccountEpoch_IsAccepted()
    {
        var provider = Provider();
        var account = AccountAtEpoch(0);
        var token = IssueAccessToken(provider, account.Id, account.SessionRevocationEpoch);

        var context = await RunSeamAsync(Validate(provider, token), account);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task TokenEpoch_HigherThanAccountEpoch_IsAccepted()
    {
        // A token with a newer epoch is accepted per the agreed ADR-036 model;
        // no synchronization mechanism is introduced in Q1.
        var provider = Provider();
        var account = AccountAtEpoch(0);
        var token = IssueAccessToken(provider, account.Id, 5);

        var context = await RunSeamAsync(Validate(provider, token), account);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task TokenEpoch_LowerThanAccountEpoch_IsRejected()
    {
        var provider = Provider();
        var account = AccountAtEpoch(3);
        var token = IssueAccessToken(provider, account.Id, 1);

        var context = await RunSeamAsync(Validate(provider, token), account);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task LegacyToken_WithoutSre_AcceptedWhileAccountAtZero()
    {
        var provider = Provider();
        var account = AccountAtEpoch(0);
        var token = Sign(provider, payload => payload["sub"] = account.Id.ToString());

        var context = await RunSeamAsync(Validate(provider, token), account);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task LegacyToken_WithoutSre_RejectedOnceAccountEpochAdvances()
    {
        var provider = Provider();
        var account = AccountAtEpoch(1);
        var token = Sign(provider, payload => payload["sub"] = account.Id.ToString());

        var context = await RunSeamAsync(Validate(provider, token), account);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task NonNumericStringSre_FailsClosed()
    {
        var provider = Provider();
        var account = AccountAtEpoch(0);
        var token = Sign(provider, payload =>
        {
            payload["sub"] = account.Id.ToString();
            payload["sre"] = "abc";
        });

        var context = await RunSeamAsync(Validate(provider, token), account);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task NumericStringSreRepresentation_FailsClosed()
    {
        var provider = Provider();
        var account = AccountAtEpoch(0);
        var token = Sign(provider, payload =>
        {
            payload["sub"] = account.Id.ToString();
            payload["sre"] = "5";
        });

        var context = await RunSeamAsync(Validate(provider, token), account);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task NonIntegralNumericSre_FailsClosed()
    {
        var provider = Provider();
        var account = AccountAtEpoch(0);
        var token = Sign(provider, payload =>
        {
            payload["sub"] = account.Id.ToString();
            payload["sre"] = 3.5;
        });

        var context = await RunSeamAsync(Validate(provider, token), account);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task NegativeTokenEpoch_DoesNotBypassRevocation()
    {
        var provider = Provider();
        var account = AccountAtEpoch(0);
        var token = Sign(provider, payload =>
        {
            payload["sub"] = account.Id.ToString();
            payload["sre"] = -1;
        });

        var context = await RunSeamAsync(Validate(provider, token), account);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task UnknownAccount_FailsClosed()
    {
        var provider = Provider();
        var token = IssueAccessToken(provider, AccountId, 0);

        var context = await RunSeamAsync(Validate(provider, token), account: null);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task MissingSubject_FailsClosed()
    {
        var provider = Provider();
        var token = Sign(provider, payload =>
        {
            payload["sre"] = 0;
            payload["email"] = "user@example.com";
        });

        var context = await RunSeamAsync(Validate(provider, token), AccountAtEpoch(0));

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task MalformedSubjectGuid_FailsClosed()
    {
        var provider = Provider();
        var token = Sign(provider, payload => payload["sub"] = "not-a-guid");

        var context = await RunSeamAsync(Validate(provider, token), AccountAtEpoch(0));

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
    }
}