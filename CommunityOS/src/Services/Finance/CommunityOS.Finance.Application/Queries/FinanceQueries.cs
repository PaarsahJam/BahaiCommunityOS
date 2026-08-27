using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Finance.Application.Authorization;
using CommunityOS.Finance.Application.DTOs;
using CommunityOS.Finance.Application.Permissions;
using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.Exceptions;
using CommunityOS.Finance.Domain.Repositories;
using MediatR;

namespace CommunityOS.Finance.Application.Queries;

public sealed record ListFundsQuery(
    Guid ActorId,
    Guid? OrganizationUnitId) : IRequest<IReadOnlyList<FundSummaryDto>>;

internal sealed class ListFundsQueryHandler(
    IFundRepository funds,
    IFinancialTransactionRepository transactions,
    AuthorizationGuard guard,
    IAuthorizationEvaluator evaluator)
    : IRequestHandler<ListFundsQuery, IReadOnlyList<FundSummaryDto>>
{
    public async Task<IReadOnlyList<FundSummaryDto>> Handle(ListFundsQuery query, CancellationToken ct)
    {
        await guard.RequireAsync(query.ActorId, FinancePermissions.FundRead,
            FinanceAuthorization.ResourceContext("fund", query.OrganizationUnitId), ct);

        var candidates = query.OrganizationUnitId is { } filterUnit
            ? await funds.ListAsync(filterUnit, ct)
            : await funds.ListAsync(organizationUnitId: null, ct);

        // Fail-closed read filtering at the query boundary: only funds the
        // caller may read are returned; nothing reveals the existence or
        // count of unauthorized funds (ADR-032). A fund is readable when the
        // caller holds the read permission at its organizational scope.
        var requests = new List<AuthorizationRequest>();
        foreach (var fund in candidates)
            requests.Add(new AuthorizationRequest(query.ActorId, FinancePermissions.FundRead, FinanceAuthorization.ForFund(fund)));

        var decisions = await evaluator.EvaluateBatchAsync(requests, ct);

        var allowed = candidates
            .Where((_, i) => decisions[i].Allowed)
            .OrderByDescending(f => f.UpdatedOn)
            .ToList();

        // The derived approved balance is computed per fund on read; it is
        // never stored (ADR-032).
        var balances = new Dictionary<Guid, long>();
        foreach (var fund in allowed)
            balances[fund.Id] = await transactions.GetApprovedBalanceMinorUnitsAsync(fund.Id, ct);

        return allowed
            .Select(f => f.ToSummaryDto(balances[f.Id]))
            .ToList()
            .AsReadOnly();
    }
}

public sealed record GetFundQuery(Guid ActorId, Guid FundId) : IRequest<FundDto>;

internal sealed class GetFundQueryHandler(
    IFundRepository funds,
    IFinancialTransactionRepository transactions,
    AuthorizationGuard guard)
    : IRequestHandler<GetFundQuery, FundDto>
{
    public async Task<FundDto> Handle(GetFundQuery query, CancellationToken ct)
    {
        var fund = await LoadOrNothingAsync(funds, query.FundId, ct);

        if (!await FinanceAuthorization.HasForFundAsync(
                guard, query.ActorId, FinancePermissions.FundRead, fund, ct))
            throw new FundNotFoundException(query.FundId);

        var balance = await transactions.GetApprovedBalanceMinorUnitsAsync(fund.Id, ct);
        return fund.ToDto(balance);
    }

    internal static async Task<Fund> LoadOrNothingAsync(
        IFundRepository funds, Guid fundId, CancellationToken ct) =>
        await funds.GetByIdAsync(fundId, ct) ?? throw new FundNotFoundException(fundId);
}

public sealed record GetFundBalanceQuery(Guid ActorId, Guid FundId) : IRequest<FundBalanceDto>;

internal sealed class GetFundBalanceQueryHandler(
    IFundRepository funds,
    IFinancialTransactionRepository transactions,
    AuthorizationGuard guard)
    : IRequestHandler<GetFundBalanceQuery, FundBalanceDto>
{
    public async Task<FundBalanceDto> Handle(GetFundBalanceQuery query, CancellationToken ct)
    {
        var fund = await GetFundQueryHandler.LoadOrNothingAsync(funds, query.FundId, ct);

        if (!await FinanceAuthorization.HasForFundAsync(
                guard, query.ActorId, FinancePermissions.TransactionRead, fund, ct))
            throw new FundNotFoundException(query.FundId);

        var balance = await transactions.GetApprovedBalanceMinorUnitsAsync(fund.Id, ct);
        return new FundBalanceDto(fund.Currency, balance);
    }
}

public sealed record ListTransactionsQuery(
    Guid ActorId,
    Guid FundId,
    string? Status) : IRequest<IReadOnlyList<FinancialTransactionDto>>;

internal sealed class ListTransactionsQueryHandler(
    IFundRepository funds,
    IFinancialTransactionRepository transactions,
    AuthorizationGuard guard)
    : IRequestHandler<ListTransactionsQuery, IReadOnlyList<FinancialTransactionDto>>
{
    public async Task<IReadOnlyList<FinancialTransactionDto>> Handle(
        ListTransactionsQuery query, CancellationToken ct)
    {
        var fund = await GetFundQueryHandler.LoadOrNothingAsync(funds, query.FundId, ct);

        await FinanceAuthorization.RequireForFundAsync(
            guard, query.ActorId, FinancePermissions.TransactionRead, fund, ct);

        var status = ResolveStatus(query.Status);

        return (await transactions.ListByFundAsync(query.FundId, status, cancellationToken: ct))
            .OrderByDescending(t => t.OccurredOn)
            .Select(t => t.ToDto())
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Unknown status names match nothing (fail-soft, never a server error);
    /// the status vocabulary is closed by ADR-032.
    /// </summary>
    private static FinancialTransactionStatus? ResolveStatus(string? status) =>
        string.IsNullOrWhiteSpace(status)
            ? null
            : FinancialTransactionStatus.All.FirstOrDefault(s =>
                string.Equals(s.Name, status, StringComparison.OrdinalIgnoreCase));
}

public sealed record GetTransactionQuery(Guid ActorId, Guid TransactionId)
    : IRequest<FinancialTransactionDto>;

internal sealed class GetTransactionQueryHandler(
    IFinancialTransactionRepository transactions,
    AuthorizationGuard guard)
    : IRequestHandler<GetTransactionQuery, FinancialTransactionDto>
{
    public async Task<FinancialTransactionDto> Handle(GetTransactionQuery query, CancellationToken ct)
    {
        var transaction = await transactions.GetByIdAsync(query.TransactionId, ct)
            ?? throw new FinancialTransactionNotFoundException(query.TransactionId);

        if (!await FinanceAuthorization.HasAtAsync(guard, query.ActorId,
                FinancePermissions.TransactionRead, FinanceAuthorization.ForTransaction(transaction), ct))
            throw new FinancialTransactionNotFoundException(query.TransactionId);

        return transaction.ToDto();
    }
}