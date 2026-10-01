using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Application.Queries;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using System.Reflection;

namespace CommunityOS.Identity.Tests.Application;

public sealed class ListSessionsQueryTests
{
    private const string DeviceName = "Back office terminal";
    private const string DevicePlatform = "Windows";

    [Fact]
    public async Task Handle_IncludesDeviceNameAndPlatform_FromOwnedDevice()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        var device = account.RegisterDevice(DeviceName, DevicePlatform, "Mozilla/5.0");
        var session = Session.Create(account.Id, device.Id, "refresh-token-hash", TimeSpan.FromHours(1));

        var handler = Handler(account, [session]);

        var result = await handler.Handle(new ListSessionsQuery(account.Id), CancellationToken.None);

        result.Single().DeviceName.Should().Be(DeviceName);
        result.Single().DevicePlatform.Should().Be(DevicePlatform);
    }

    [Fact]
    public async Task Handle_PreservesExistingSessionFields()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        var device = account.RegisterDevice(DeviceName, DevicePlatform, null);
        var session = Session.Create(account.Id, device.Id, "refresh-token-hash", TimeSpan.FromHours(1));

        var handler = Handler(account, [session]);

        var result = await handler.Handle(new ListSessionsQuery(account.Id), CancellationToken.None);

        var dto = result.Single();
        dto.Id.Should().Be(session.Id);
        dto.DeviceId.Should().Be(device.Id);
        dto.CreatedOn.Should().Be(session.CreatedOn);
        dto.ExpiresOn.Should().Be(session.ExpiresOn);
        dto.LastUsedOn.Should().Be(session.LastUsedOn);
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MissingDeviceMetadata_IsNullAndNeverFabricated()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        // A session whose device id does not exist in the actor's own device
        // collection: the metadata is absent, never invented.
        var session = Session.Create(account.Id, Guid.NewGuid(), "refresh-token-hash", TimeSpan.FromHours(1));

        var handler = Handler(account, [session]);

        var result = await handler.Handle(new ListSessionsQuery(account.Id), CancellationToken.None);

        result.Single().DeviceName.Should().BeNull();
        result.Single().DevicePlatform.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NeverResolvesAnotherAccountsDevice()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        var otherAccount = UserAccount.Register(Email.Create("grace@example.org"), "password-hash");
        var foreignDevice = otherAccount.RegisterDevice("Grace's phone", "Android", null);
        // The session row names a device owned by a different account; the
        // handler resolves device metadata only from the actor's own aggregate.
        var session = Session.Create(account.Id, foreignDevice.Id, "refresh-token-hash", TimeSpan.FromHours(1));

        var handler = Handler(account, [session]);

        var result = await handler.Handle(new ListSessionsQuery(account.Id), CancellationToken.None);

        result.Single().DeviceName.Should().BeNull();
        result.Single().DevicePlatform.Should().BeNull();
    }

    [Fact]
    public async Task Handle_QueriesOnlyTheActorsAccountAndSessions()
    {
        var actorId = Guid.NewGuid();
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetActiveByUserAsync(actorId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Session>());

        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdAsync(actorId, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new ListSessionsQueryHandler(sessions, userAccounts);
        await handler.Handle(new ListSessionsQuery(actorId), CancellationToken.None);

        await userAccounts.Received(1).GetByIdAsync(actorId, Arg.Any<CancellationToken>());
        await sessions.Received(1).GetActiveByUserAsync(actorId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExpiredButUnrevokedSession_StillReturnsWithIsActiveFalse()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        var device = account.RegisterDevice(DeviceName, DevicePlatform, null);
        var session = Session.Create(account.Id, device.Id, "refresh-token-hash", TimeSpan.FromHours(1));
        MakeExpired(session);

        var handler = Handler(account, [session]);

        var result = await handler.Handle(new ListSessionsQuery(account.Id), CancellationToken.None);

        result.Should().HaveCount(1);
        result.Single().IsActive.Should().BeFalse();
        // Metadata is still resolved even for expired-but-unrevoked rows.
        result.Single().DeviceName.Should().Be(DeviceName);
    }

    [Fact]
    public async Task Handle_PreservesRepositoryOrdering()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        var device = account.RegisterDevice(DeviceName, DevicePlatform, null);
        var first = Session.Create(account.Id, device.Id, "hash-1", TimeSpan.FromHours(1));
        var second = Session.Create(account.Id, device.Id, "hash-2", TimeSpan.FromHours(1));

        var handler = Handler(account, [first, second]);

        var result = await handler.Handle(new ListSessionsQuery(account.Id), CancellationToken.None);

        result.Select(x => x.Id).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task Handle_SameFamilyMarkedCurrent_OthersNot()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        var device = account.RegisterDevice(DeviceName, DevicePlatform, null);
        var first = Session.Create(account.Id, device.Id, "hash-1", TimeSpan.FromHours(1));
        var second = Session.Create(account.Id, device.Id, "hash-2", TimeSpan.FromHours(1));

        var handler = Handler(account, [first, second]);

        var result = await handler.Handle(
            new ListSessionsQuery(account.Id, first.TokenFamilyId), CancellationToken.None);

        result.ElementAt(0).IsCurrent.Should().BeTrue();
        result.ElementAt(1).IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EveryRowInTheFamily_IsMarkedCurrent()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        var device = account.RegisterDevice(DeviceName, DevicePlatform, null);
        var original = Session.Create(account.Id, device.Id, "hash-1", TimeSpan.FromHours(1));
        // A rotated row shares the same logical family; the superseded row is
        // still returned and, being family-scoped, is equally current.
        var rotated = original.Rotate("hash-2", TimeSpan.FromHours(1), sessionRevocationEpochAtIssue: 0);

        var handler = Handler(account, [original, rotated]);

        var result = await handler.Handle(
            new ListSessionsQuery(account.Id, original.TokenFamilyId), CancellationToken.None);

        result.Should().OnlyContain(x => x.IsCurrent);
    }

    [Fact]
    public async Task Handle_MissingClaim_IsCurrentFalseForEveryRow()
    {
        var account = UserAccount.Register(Email.Create("ada@example.org"), "password-hash");
        var device = account.RegisterDevice(DeviceName, DevicePlatform, null);
        var first = Session.Create(account.Id, device.Id, "hash-1", TimeSpan.FromHours(1));
        var second = Session.Create(account.Id, device.Id, "hash-2", TimeSpan.FromHours(1));

        var handler = Handler(account, [first, second]);

        var result = await handler.Handle(new ListSessionsQuery(account.Id), CancellationToken.None);

        result.Should().OnlyContain(x => !x.IsCurrent);
    }

    [Fact]
    public async Task Handle_UnknownAccount_ThrowsNotFound()
    {
        var sessions = Substitute.For<ISessionRepository>();
        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((UserAccount?)null);

        var handler = new ListSessionsQueryHandler(sessions, userAccounts);

        var act = async () => await handler.Handle(
            new ListSessionsQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UserAccountNotFoundException>();
        await sessions.DidNotReceiveWithAnyArgs()
            .GetActiveByUserAsync(default, default);
    }

    private static ListSessionsQueryHandler Handler(UserAccount account, IReadOnlyList<Session> sessionRows)
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetActiveByUserAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(sessionRows);

        var userAccounts = Substitute.For<IUserAccountRepository>();
        userAccounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        return new ListSessionsQueryHandler(sessions, userAccounts);
    }

    private static void MakeExpired(Session session)
    {
        var field = typeof(Session).GetField("<ExpiresOn>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Session.ExpiresOn backing field not found.");

        field.SetValue(session, DateTime.UtcNow.AddMinutes(-1));
    }
}