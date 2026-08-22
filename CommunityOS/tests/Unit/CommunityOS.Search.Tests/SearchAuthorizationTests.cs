using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Search.Application;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CommunityOS.Search.Tests;
public sealed class SearchAuthorizationTests
{
    private static readonly SearchOptions Settings = new();

    private static IAuthorizationEvaluator Evaluator(Func<AuthorizationRequest, bool> allows)
    {
        var evaluator = Substitute.For<IAuthorizationEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => allows(callInfo.Arg<AuthorizationRequest>())
                ? AuthorizationDecision.Allow("grant", ["grant"], DateTime.UtcNow)
                : AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.ScopeMismatch, DateTime.UtcNow));
        evaluator.EvaluateBatchAsync(Arg.Any<IReadOnlyList<AuthorizationRequest>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<IReadOnlyList<AuthorizationRequest>>()
                .Select(r => allows(r)
                    ? AuthorizationDecision.Allow("grant", ["grant"], DateTime.UtcNow)
                    : AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.ScopeMismatch, DateTime.UtcNow))
                .ToList());
        return evaluator;
    }

    private static IAuthorizationEvaluator AlwaysAllowing()
    {
        var evaluator = Substitute.For<IAuthorizationEvaluator>();
        var allow = AuthorizationDecision.Allow("grant", ["grant"], DateTime.UtcNow);
        evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>()).Returns(allow);
        evaluator.EvaluateBatchAsync(Arg.Any<IReadOnlyList<AuthorizationRequest>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<IReadOnlyList<AuthorizationRequest>>().Select(_ => allow).ToList());
        return evaluator;
    }

    private static SearchProjectionRow Row(Guid? unit = null, Guid[]? scopes = null, bool sensitive = false) =>
        new(Guid.NewGuid(), "record", Guid.NewGuid(), "title", "code", "Active", sensitive, unit,
            scopes ?? Array.Empty<Guid>(), DateTime.UtcNow, DateTime.UtcNow, 0.5);

    private static SearchQueryHandler Handler(IAuthorizationEvaluator evaluator, ISearchProjectionRepository repository) =>
        new(repository, new AuthorizationGuard(evaluator), evaluator, Options.Create(Settings));

    [Fact]
    public async Task SuppliedScopeDenialFailsClosed()
    {
        var evaluator = Substitute.For<IAuthorizationEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Deny("d", AuthorizationDecisionReason.ScopeMismatch, DateTime.UtcNow));
        var repository = Substitute.For<ISearchProjectionRepository>();
        var handler = Handler(evaluator, repository);
        var action = () => handler.Handle(new SearchQuery(Guid.NewGuid(), "term", null, null, Guid.NewGuid(), null, false, 25, 0), CancellationToken.None);
        await action.Should().ThrowAsync<AuthorizationForbiddenException>();
        await repository.DidNotReceiveWithAnyArgs().FetchCandidatesAsync(default!, default, default, default);
    }

    [Fact]
    public async Task RowsOutsideGrantedScopesAreSilentlyDropped()
    {
        var grantedUnit = Guid.NewGuid(); var otherUnit = Guid.NewGuid();
        var visible = Row(grantedUnit); var hidden = Row(otherUnit); var unscoped = Row();
        var repository = Substitute.For<ISearchProjectionRepository>();
        repository.FetchCandidatesAsync(Arg.Any<SearchFilters>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { visible, hidden, unscoped });
        var evaluator = Evaluator(r =>
            r.Permission == "search.result.read" && r.Context.IsGlobal ||
            r.Context.OrganizationUnitId == grantedUnit ||
            r.Context.ResourceType is not null && r.Context.ResourceId == unscoped.SourceId);

        var page = await Handler(evaluator, repository).Handle(
            new SearchQuery(Guid.NewGuid(), "term", null, null, null, null, false, 25, 0), CancellationToken.None);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo([visible.Id, unscoped.Id]);
        page.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task SensitiveRowRequiresSensitiveCapabilityAtRowScope()
    {
        // Caller reads units A and B but holds the sensitive capability at A
        // only: a sensitive row at B stays invisible even though it passed the
        // ordinary read pass (ADR-026).
        var unitA = Guid.NewGuid(); var unitB = Guid.NewGuid();
        var plainA = Row(unitA); var sensitiveA = Row(unitA, sensitive: true); var sensitiveB = Row(unitB, sensitive: true);
        var repository = Substitute.For<ISearchProjectionRepository>();
        repository.FetchCandidatesAsync(Arg.Any<SearchFilters>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { plainA, sensitiveA, sensitiveB });
        var evaluator = Evaluator(r => r.Permission switch
        {
            "search.result.read" => r.Context.IsGlobal ||
                r.Context.OrganizationUnitId is var u && (u == unitA || u == unitB),
            "search.result.read.sensitive" => r.Context.IsGlobal || r.Context.OrganizationUnitId == unitA,
            _ => false
        });

        var page = await Handler(evaluator, repository).Handle(
            new SearchQuery(Guid.NewGuid(), "term", null, null, null, null, true, 25, 0), CancellationToken.None);

        page.Items.Select(x => x.Id).Should().BeEquivalentTo([plainA.Id, sensitiveA.Id]);
        page.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task AdditionalScopesAuthorizeARow()
    {
        var primary = Guid.NewGuid(); var additional = Guid.NewGuid();
        var row = Row(primary, [additional]);
        var repository = Substitute.For<ISearchProjectionRepository>();
        repository.FetchCandidatesAsync(Arg.Any<SearchFilters>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { row });
        // Caller holds nothing at the primary scope but does at an additional
        // scope; the any-of rule makes the row visible.
        var evaluator = Evaluator(r =>
            r.Permission == "search.result.read" && r.Context.IsGlobal ||
            r.Context.OrganizationUnitId == additional);

        var page = await Handler(evaluator, repository).Handle(
            new SearchQuery(Guid.NewGuid(), "term", null, null, null, null, false, 25, 0), CancellationToken.None);

        page.Items.Should().ContainSingle(x => x.Id == row.Id);
    }

    [Fact]
    public async Task PaginationSlicesAuthorizedRowsOnly()
    {
        var unit = Guid.NewGuid();
        var authorized = Enumerable.Range(0, 5).Select(_ => Row(unit)).ToList();
        var repository = Substitute.For<ISearchProjectionRepository>();
        repository.FetchCandidatesAsync(Arg.Any<SearchFilters>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(authorized);

        var page = await Handler(AlwaysAllowing(), repository).Handle(
            new SearchQuery(Guid.NewGuid(), "term", null, null, null, null, false, 2, 1), CancellationToken.None);

        page.Limit.Should().Be(2); page.Offset.Should().Be(1); page.TotalCount.Should().Be(5);
        page.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task LimitAboveMaximumIsClampedToConfiguredMax()
    {
        var unit = Guid.NewGuid();
        var authorized = Enumerable.Range(0, 10).Select(_ => Row(unit)).ToList();
        var repository = Substitute.For<ISearchProjectionRepository>();
        repository.FetchCandidatesAsync(Arg.Any<SearchFilters>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(authorized);

        var page = await Handler(AlwaysAllowing(), repository).Handle(
            new SearchQuery(Guid.NewGuid(), "term", null, null, null, null, false, 500, 0), CancellationToken.None);

        page.Limit.Should().Be(50);
        page.Items.Should().HaveCount(10);
        page.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task MissingActorFailsBeforeAnyDataAccess()
    {
        var repository = Substitute.For<ISearchProjectionRepository>();
        var action = () => Handler(AlwaysAllowing(), repository).Handle(
            new SearchQuery(Guid.Empty, "term", null, null, null, null, false, 25, 0), CancellationToken.None);
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        await repository.DidNotReceiveWithAnyArgs().FetchCandidatesAsync(default!, default, default, default);
    }

    [Fact]
    public async Task ReindexRequiresIndexManageCapability()
    {
        var evaluator = Substitute.For<IAuthorizationEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Deny("d", AuthorizationDecisionReason.ScopeMismatch, DateTime.UtcNow));
        var reconciler = Substitute.For<IProjectionReconciler>();
        var handler = new ReindexCommandHandler(new AuthorizationGuard(evaluator), reconciler, NullLogger<ReindexCommandHandler>.Instance);
        var action = () => handler.Handle(new ReindexCommand(Guid.NewGuid()), CancellationToken.None);
        await action.Should().ThrowAsync<AuthorizationForbiddenException>();
        await reconciler.DidNotReceiveWithAnyArgs().ReconcileAsync(default, default);
    }
}
