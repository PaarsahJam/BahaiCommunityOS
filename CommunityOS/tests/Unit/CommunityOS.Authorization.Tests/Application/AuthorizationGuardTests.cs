using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using NSubstitute;

namespace CommunityOS.Authorization.Tests.Application;

public class AuthorizationGuardTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    private static (AuthorizationGuard Guard, IAuthorizationEvaluator Evaluator) CreateGuard(
        AuthorizationDecision decision)
    {
        var evaluator = Substitute.For<IAuthorizationEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(decision);
        return (new AuthorizationGuard(evaluator), evaluator);
    }

    [Fact]
    public async Task HasAsync_returns_true_when_decision_allowed()
    {
        var (guard, evaluator) = CreateGuard(
            AuthorizationDecision.Allow("1", ["role:admin:1"], DateTime.UtcNow));

        var allowed = await guard.HasAsync(Actor, "authz.role.assign", new AuthorizationContext());

        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task HasAsync_returns_false_when_decision_denied()
    {
        var (guard, _) = CreateGuard(
            AuthorizationDecision.Deny("1", AuthorizationDecisionReason.DeniedByDefault, DateTime.UtcNow));

        var allowed = await guard.HasAsync(Actor, "authz.role.assign", new AuthorizationContext());

        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task RequireAsync_throws_forbidden_when_denied()
    {
        var (guard, _) = CreateGuard(
            AuthorizationDecision.Deny("1", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow));

        var act = () => guard.RequireAsync(Actor, "authz.role.assign", new AuthorizationContext());

        await act.Should().ThrowAsync<AuthorizationForbiddenException>()
            .Where(e => e.Message.Contains("authz.role.assign"));
    }

    [Fact]
    public async Task RequireAsync_does_not_throw_when_allowed()
    {
        var (guard, _) = CreateGuard(
            AuthorizationDecision.Allow("1", ["role:admin:1"], DateTime.UtcNow));

        var act = () => guard.RequireAsync(Actor, "authz.role.assign", new AuthorizationContext());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Guard_uses_empty_context_when_none_supplied()
    {
        var evaluator = Substitute.For<IAuthorizationEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Allow("1", ["role:admin:1"], DateTime.UtcNow));
        var guard = new AuthorizationGuard(evaluator);

        await guard.RequireAsync(Actor, "authz.role.assign");

        await evaluator.Received(1).EvaluateAsync(
            Arg.Is<AuthorizationRequest>(r => r.Context == AuthorizationContext.Empty),
            Arg.Any<CancellationToken>());
    }
}
