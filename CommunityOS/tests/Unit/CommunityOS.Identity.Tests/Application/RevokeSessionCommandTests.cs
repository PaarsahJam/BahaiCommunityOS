using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Exceptions;
using CommunityOS.Identity.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using System.Reflection;

namespace CommunityOS.Identity.Tests.Application;

public sealed class RevokeSessionCommandTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid OtherAccountId = Guid.NewGuid();
    private static readonly Guid DeviceId = Guid.NewGuid();

    private static Session ActiveSession(Guid userAccountId) =>
        Session.Create(userAccountId, DeviceId, "refresh-token-hash", TimeSpan.FromHours(1));

    [Fact]
    public async Task Handle_RevokesTheCallersOwnActiveSession_AndPersistsRevocation()
    {
        var session = ActiveSession(ActorId);
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var handler = new RevokeSessionCommandHandler(sessions);
        await handler.Handle(new RevokeSessionCommand(ActorId, session.Id), CancellationToken.None);

        session.IsRevoked.Should().BeTrue();
        session.RevokedOn.Should().NotBeNull();
        session.RevocationReason.Should().Be("User revoked session.");
        await sessions.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownSession_ThrowsNotFoundAndPersistsNothing()
    {
        var sessionId = Guid.NewGuid();
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns((Session?)null);

        var handler = new RevokeSessionCommandHandler(sessions);
        var act = async () => await handler.Handle(
            new RevokeSessionCommand(ActorId, sessionId), CancellationToken.None);

        await act.Should().ThrowAsync<SessionNotFoundException>();
        await sessions.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Handle_AnotherAccountsSession_ThrowsNotFoundAndNeverMutatesThatSession()
    {
        var session = ActiveSession(OtherAccountId);
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var handler = new RevokeSessionCommandHandler(sessions);
        var act = async () => await handler.Handle(
            new RevokeSessionCommand(ActorId, session.Id), CancellationToken.None);

        await act.Should().ThrowAsync<SessionNotFoundException>();
        session.IsRevoked.Should().BeFalse();
        session.RevokedOn.Should().BeNull();
        await sessions.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Handle_AlreadyRevokedSession_IsIdempotent()
    {
        var session = ActiveSession(ActorId);
        session.Revoke("User logout.");
        var revokedOnBefore = session.RevokedOn!.Value;

        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var handler = new RevokeSessionCommandHandler(sessions);
        await handler.Handle(new RevokeSessionCommand(ActorId, session.Id), CancellationToken.None);

        session.IsRevoked.Should().BeTrue();
        session.RevokedOn.Should().Be(revokedOnBefore);
        session.RevocationReason.Should().Be("User logout.");
        await sessions.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExpiredOwnedSession_StillRecordsTheRevocation()
    {
        var session = ActiveSession(ActorId);
        MakeExpired(session);
        session.IsExpired.Should().BeTrue();

        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var handler = new RevokeSessionCommandHandler(sessions);
        await handler.Handle(new RevokeSessionCommand(ActorId, session.Id), CancellationToken.None);

        session.IsRevoked.Should().BeTrue();
        session.RevokedOn.Should().NotBeNull();
        session.RevocationReason.Should().Be("User revoked session.");
        await sessions.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RevokesOnlyTheRequestedSession_AndNeverRevokesAllForUser()
    {
        var target = ActiveSession(ActorId);
        var other = ActiveSession(ActorId);

        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        var handler = new RevokeSessionCommandHandler(sessions);
        await handler.Handle(new RevokeSessionCommand(ActorId, target.Id), CancellationToken.None);

        target.IsRevoked.Should().BeTrue();
        other.IsRevoked.Should().BeFalse();
        other.RevokedOn.Should().BeNull();
        await sessions.DidNotReceiveWithAnyArgs().RevokeAllForUserAsync(default, default!, default);
    }

    internal static void MakeExpired(Session session)
    {
        var field = typeof(Session).GetField("<ExpiresOn>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Session.ExpiresOn backing field not found.");

        field.SetValue(session, DateTime.UtcNow.AddMinutes(-1));
    }
}