using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.Validators;
using CommunityOS.Identity.Domain.Repositories;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace CommunityOS.Identity.Tests.Application;

public sealed class RevokeOthersCommandTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid CurrentFamilyId = Guid.NewGuid();

    private const string ApprovedReason = "User revoked other sessions.";

    private static (RevokeOthersCommandHandler handler, ISessionRepository sessions)
        Handler()
    {
        var sessions = Substitute.For<ISessionRepository>();
        var handler = new RevokeOthersCommandHandler(sessions);
        return (handler, sessions);
    }

    [Fact]
    public async Task Handle_OperationReceives_AuthenticatedUserId()
    {
        var (handler, sessions) = Handler();

        await handler.Handle(
            new RevokeOthersCommand(ActorId, CurrentFamilyId), CancellationToken.None);

        await sessions.Received(1).RevokeAllExceptFamilyForUserAsync(
            Arg.Is<Guid>(id => id == ActorId),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OperationReceives_CurrentFamilyId()
    {
        var (handler, sessions) = Handler();

        await handler.Handle(
            new RevokeOthersCommand(ActorId, CurrentFamilyId), CancellationToken.None);

        await sessions.Received(1).RevokeAllExceptFamilyForUserAsync(
            Arg.Any<Guid>(),
            Arg.Is<Guid>(familyId => familyId == CurrentFamilyId),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OperationReceives_ExactApprovedReason()
    {
        var (handler, sessions) = Handler();

        await handler.Handle(
            new RevokeOthersCommand(ActorId, CurrentFamilyId), CancellationToken.None);

        await sessions.Received(1).RevokeAllExceptFamilyForUserAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Is<string>(reason => reason == ApprovedReason),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvokesNewRepositoryOperation_ExactlyOnce()
    {
        var (handler, sessions) = Handler();

        await handler.Handle(
            new RevokeOthersCommand(ActorId, CurrentFamilyId), CancellationToken.None);

        await sessions.Received(1).RevokeAllExceptFamilyForUserAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NeverPerformsAccountWideRevoke()
    {
        var (handler, sessions) = Handler();

        await handler.Handle(
            new RevokeOthersCommand(ActorId, CurrentFamilyId), CancellationToken.None);

        await sessions.DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default!, default);
    }

    [Fact]
    public async Task Handle_EmitsNoSecurityEvent()
    {
        // A security event could only be written through a repository
        // dependency. The handler depends solely on ISessionRepository, so
        // there is no path for it to record a security event.
        var (handler, _) = Handler();

        await handler.Handle(
            new RevokeOthersCommand(ActorId, CurrentFamilyId), CancellationToken.None);

        var dependencies = typeof(RevokeOthersCommandHandler).GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType);

        dependencies.Should().ContainSingle()
            .Which.Should().Be<ISessionRepository>();
    }

    [Fact]
    public void Command_CarriesOnlyAuthenticatedActorAndCurrentFamily()
    {
        typeof(RevokeOthersCommand).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(new[]
            {
                nameof(RevokeOthersCommand.UserAccountId),
                nameof(RevokeOthersCommand.CurrentTokenFamilyId)
            });
    }

    [Fact]
    public void Command_CurrentFamilyId_IsNeverNullable()
    {
        typeof(RevokeOthersCommand)
            .GetProperty(nameof(RevokeOthersCommand.CurrentTokenFamilyId))!
            .PropertyType.Should().Be<Guid>();
    }

    [Fact]
    public void Command_ReturnsNoResponsePayload()
    {
        var commandType = typeof(RevokeOthersCommand);
        commandType.GetInterfaces().Should().Contain(typeof(IRequest));
        // A unary IRequest<TResponse> would carry a response payload; the
        // handler contract here is a void command.
        commandType.GetInterfaces().Should().NotContain(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
    }

    [Fact]
    public void Validator_RejectsEmptyUserAccountId()
    {
        var validator = new RevokeOthersCommandValidator();

        var result = validator.Validate(
            new RevokeOthersCommand(Guid.Empty, CurrentFamilyId));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(
            e => e.PropertyName == nameof(RevokeOthersCommand.UserAccountId));
    }

    [Fact]
    public void Validator_RejectsEmptyCurrentFamilyId()
    {
        var validator = new RevokeOthersCommandValidator();

        var result = validator.Validate(
            new RevokeOthersCommand(ActorId, Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(
            e => e.PropertyName == nameof(RevokeOthersCommand.CurrentTokenFamilyId));
    }

    [Fact]
    public void Validator_AcceptsActingUserAndCurrentFamily()
    {
        var validator = new RevokeOthersCommandValidator();

        var result = validator.Validate(
            new RevokeOthersCommand(ActorId, CurrentFamilyId));

        result.IsValid.Should().BeTrue();
    }
}