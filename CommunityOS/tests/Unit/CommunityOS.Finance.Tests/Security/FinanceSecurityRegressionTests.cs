using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Finance.Application;
using CommunityOS.Finance.Application.Commands;
using CommunityOS.Finance.Application.Permissions;
using CommunityOS.Finance.Application.Queries;
using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.Exceptions;
using CommunityOS.Finance.Domain.Repositories;
using CommunityOS.Finance.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CommunityOS.Finance.Tests.Security;

/// <summary>
/// Security regression tests for the Finance bounded context (ADR-032). These
/// lock in the authorization boundaries: every command and guarded query flows
/// through the Authorization guard, the guard is fail-closed (a denied or
/// unreachable evaluator blocks the operation and nothing is persisted),
/// denied reads are indistinguishable from not-found (no enumeration oracle),
/// and list reads are filtered fail-closed at the query boundary.
/// </summary>
public class FinanceSecurityRegressionTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private sealed class Harness
    {
        public RepoSet Repos { get; } = RepoSet.Create();
        public ServiceProvider Provider { get; }

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddFinanceApplication();

            services.AddScoped(_ => Repos.Funds);
            services.AddScoped(_ => Repos.Transactions);
            services.AddScoped(_ => Repos.Units);
            services.AddScoped(_ => Repos.Evaluator);

            Provider = services.BuildServiceProvider();
        }

        public ISender Sender => Provider.GetRequiredService<ISender>();

        /// <summary>Denies every authorization request (fail-closed default).</summary>
        public void DenyAll() => Repos.Evaluator.EvaluateAsync(
                Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, Now));

        /// <summary>Allows a single permission, optionally scoped to a resource.</summary>
        public void Allow(string permission, Guid? resourceId = null, Guid? unitId = null) =>
            Repos.Evaluator.EvaluateAsync(
                    Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var r = call.Arg<AuthorizationRequest>();
                    if (r.Permission != permission) return Deny();
                    if (resourceId is { } id && r.Context.ResourceId != id) return Deny();
                    if (unitId is { } u && r.Context.OrganizationUnitId != u) return Deny();
                    return AuthorizationDecision.Allow("allow", [r.Permission], Now);
                });

        /// <summary>
        /// Fail-closed batch evaluation used by list filtering: the coarse
        /// scope-level read (resourceId is null) is granted for the permission,
        /// but per-resource batch checks are allowed only for the listed ids.
        /// </summary>
        public void AllowBatch(string permission, params Guid[] allowedResourceIds)
        {
            Allow(permission);
            Repos.Evaluator.EvaluateBatchAsync(
                    Arg.Any<IReadOnlyList<AuthorizationRequest>>(), Arg.Any<CancellationToken>())
                .Returns(call => call.Arg<IReadOnlyList<AuthorizationRequest>>()
                    .Select(r => allowedResourceIds.Contains(r.Context.ResourceId ?? Guid.Empty)
                        ? AuthorizationDecision.Allow("allow", [r.Permission], Now)
                        : Deny())
                    .ToList());
        }

        private static AuthorizationDecision Deny() =>
            AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, Now);
    }

    private sealed record RepoSet(
        IFundRepository Funds,
        IFinancialTransactionRepository Transactions,
        IFinanceOrganizationUnitReferenceRepository Units,
        IAuthorizationEvaluator Evaluator)
    {
        public static RepoSet Create() => new(
            Substitute.For<IFundRepository>(),
            Substitute.For<IFinancialTransactionRepository>(),
            Substitute.For<IFinanceOrganizationUnitReferenceRepository>(),
            Substitute.For<IAuthorizationEvaluator>());
    }

    private static Harness CreateHarness() => new();

    private static Fund StubFund(Guid? unitId = null) =>
        Fund.Create(Guid.NewGuid(), unitId ?? Guid.NewGuid(), "House of Justice", "Sacred Fund", "USD");

    // --- 1. Denied commands fail and persist nothing ---

    [Fact]
    public async Task CreateFund_is_denied_and_nothing_is_persisted_when_evaluator_denies()
    {
        var h = CreateHarness();
        h.DenyAll();

        var act = () => h.Sender.Send(new CreateFundCommand(
            ActorId, Guid.NewGuid(), "Sacred Fund", "USD"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Funds.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task CreateFund_is_denied_when_evaluator_is_unreachable()
    {
        var h = CreateHarness();
        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AuthorizationDecision>(new HttpRequestException("authz down")));

        var act = () => h.Sender.Send(new CreateFundCommand(
            ActorId, Guid.NewGuid(), "Sacred Fund", "USD"));

        await act.Should().ThrowAsync<Exception>();
        await h.Repos.Funds.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task CreateFund_succeeds_when_managed_and_scope_is_a_known_unit()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        h.Allow(FinancePermissions.FundManage);
        h.Repos.Units.GetByOrganizationUnitIdAsync(unit, Arg.Any<CancellationToken>())
            .Returns(OrganizationUnitReference.Create(Guid.NewGuid(), unit, "House of Justice"));

        var result = await h.Sender.Send(new CreateFundCommand(ActorId, unit, "Sacred Fund", "USD"));

        result.OrganizationUnitId.Should().Be(unit);
        result.Currency.Should().Be("USD");
        result.Status.Should().Be("active");
        result.BalanceMinorUnits.Should().Be(0);
        await h.Repos.Funds.Received(1).AddAsync(Arg.Any<Fund>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFund_rejects_an_unknown_unit_scope_even_when_managed()
    {
        var h = CreateHarness();
        var unit = Guid.NewGuid();
        h.Allow(FinancePermissions.FundManage);
        h.Repos.Units.GetByOrganizationUnitIdAsync(unit, Arg.Any<CancellationToken>())
            .Returns((OrganizationUnitReference?)null);

        var act = () => h.Sender.Send(new CreateFundCommand(ActorId, unit, "Sacred Fund", "USD"));

        await act.Should().ThrowAsync<InvalidFundScopeException>();
        await h.Repos.Funds.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    // --- 2. Denied mutations fail and persist nothing ---

    [Fact]
    public async Task RecordTransaction_is_denied_and_nothing_is_persisted_when_evaluator_denies()
    {
        var h = CreateHarness();
        var fund = StubFund();
        h.Repos.Funds.GetByIdAsync(fund.Id, Arg.Any<CancellationToken>()).Returns(fund);
        h.DenyAll();

        var act = () => h.Sender.Send(new RecordTransactionCommand(
            ActorId, fund.Id, "contribution", "USD", 100, "seeds", null));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Funds.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task RecordTransaction_rejects_a_transfer_without_a_destination()
    {
        var h = CreateHarness();
        var fund = StubFund();
        h.Repos.Funds.GetByIdAsync(fund.Id, Arg.Any<CancellationToken>()).Returns(fund);
        h.Allow(FinancePermissions.TransactionRecord);

        var act = () => h.Sender.Send(new RecordTransactionCommand(
            ActorId, fund.Id, "transfer", "USD", 100, null, null));

        await act.Should().ThrowAsync<TransferReferenceRequiredException>();
        await h.Repos.Funds.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task ApproveTransaction_is_denied_without_the_approve_capability()
    {
        var h = CreateHarness();
        var tx = RecordedAndSubmitted();
        h.Repos.Transactions.GetByIdAsync(tx.Id, Arg.Any<CancellationToken>()).Returns(tx);
        // The actor holds record (submission) but NOT the approve capability.
        h.Allow(FinancePermissions.TransactionRecord);

        var act = () => h.Sender.Send(new ApproveTransactionCommand(Guid.NewGuid(), tx.Id));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Transactions.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task ApproveTransaction_succeeds_when_approved_at_the_fund_scope()
    {
        var h = CreateHarness();
        var tx = RecordedAndSubmitted();
        var approver = Guid.NewGuid();
        h.Repos.Transactions.GetByIdAsync(tx.Id, Arg.Any<CancellationToken>()).Returns(tx);
        h.Allow(FinancePermissions.TransactionApprove,
            resourceId: tx.FundId, unitId: tx.OrganizationUnitId);

        var result = await h.Sender.Send(new ApproveTransactionCommand(approver, tx.Id));

        result.Status.Should().Be("approved");
        result.ApprovedBy.Should().Be(approver);
        await h.Repos.Transactions.Received(1).UpdateAsync(tx, Arg.Any<CancellationToken>());
    }

    // --- 3. Denied reads are indistinguishable from not-found ---

    [Fact]
    public async Task GetFund_throws_FundNotFound_when_read_is_denied()
    {
        var h = CreateHarness();
        var fund = StubFund();
        h.Repos.Funds.GetByIdAsync(fund.Id, Arg.Any<CancellationToken>()).Returns(fund);
        h.DenyAll();

        var act = () => h.Sender.Send(new GetFundQuery(ActorId, fund.Id));

        await act.Should().ThrowAsync<FundNotFoundException>();
    }

    // --- 4. List filtering is fail-closed: unauthorized funds are excluded ---

    [Fact]
    public async Task ListFunds_returns_only_funds_the_caller_may_read()
    {
        var h = CreateHarness();
        var allowed = StubFund();
        var denied = StubFund();
        h.Repos.Funds.ListAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([allowed, denied]);
        h.AllowBatch(FinancePermissions.FundRead, allowed.Id);
        h.Repos.Transactions.GetApprovedBalanceMinorUnitsAsync(allowed.Id, Arg.Any<CancellationToken>())
            .Returns(500);

        var result = await h.Sender.Send(new ListFundsQuery(ActorId, null));

        result.Should().ContainSingle();
        result.Single().Id.Should().Be(allowed.Id);
        result.Single().BalanceMinorUnits.Should().Be(500);
    }

    // --- 5. List status filtering is fail-soft: unknown statuses never error ---

    [Fact]
    public async Task ListTransactions_never_errors_and_passes_null_status_for_unknown_names()
    {
        var h = CreateHarness();
        var fund = StubFund();
        h.Repos.Funds.GetByIdAsync(fund.Id, Arg.Any<CancellationToken>()).Returns(fund);
        h.Allow(FinancePermissions.TransactionRead);
        h.Repos.Transactions.ListByFundAsync(
                Arg.Any<Guid>(), Arg.Any<FinancialTransactionStatus?>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await h.Sender.Send(new ListTransactionsQuery(ActorId, fund.Id, "not-a-status"));

        result.Should().BeEmpty();
        await h.Repos.Transactions.Received(1)
            .ListByFundAsync(fund.Id, null, cancellationToken: Arg.Any<CancellationToken>());
    }

    private static FinancialTransaction RecordedAndSubmitted()
    {
        var fund = StubFund();
        var tx = fund.RecordTransaction(
            Guid.NewGuid(), Money.Create("USD", 100), FinancialTransactionType.Contribution,
            null, null, ActorId, Now);
        tx.Submit(Guid.NewGuid(), Now);
        return tx;
    }
}