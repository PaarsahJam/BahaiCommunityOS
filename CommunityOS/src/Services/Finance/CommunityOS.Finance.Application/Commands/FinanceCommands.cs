using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Finance.Application.Authorization;
using CommunityOS.Finance.Application.DTOs;
using CommunityOS.Finance.Application.Logging;
using CommunityOS.Finance.Application.Permissions;
using CommunityOS.Finance.Application.Pipeline;
using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.Exceptions;
using CommunityOS.Finance.Domain.Repositories;
using CommunityOS.Finance.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using FinanceDomainEventPublisher = CommunityOS.Finance.Application.Pipeline.DomainEventPublisher;

namespace CommunityOS.Finance.Application.Commands;

public sealed record CreateFundCommand(
    Guid ActorId,
    Guid OrganizationUnitId,
    string Name,
    string Currency) : IRequest<FundDto>;

internal sealed class CreateFundCommandHandler(
    IFundRepository funds,
    IFinanceOrganizationUnitReferenceRepository units,
    AuthorizationGuard guard)
    : IRequestHandler<CreateFundCommand, FundDto>
{
    public async Task<FundDto> Handle(CreateFundCommand cmd, CancellationToken ct)
    {
        // Global manage capability, verified against the requested unit scope.
        await guard.RequireAsync(cmd.ActorId, FinancePermissions.FundManage,
            new AuthorizationContext(cmd.OrganizationUnitId, "fund"), ct);

        var unit = await units.GetByOrganizationUnitIdAsync(cmd.OrganizationUnitId, ct)
            ?? throw new InvalidFundScopeException(cmd.OrganizationUnitId);

        var fund = Fund.Create(
            Guid.NewGuid(),
            unit.OrganizationUnitId,
            unit.DisplayName,
            cmd.Name,
            cmd.Currency);

        await funds.AddAsync(fund, ct);

        // A brand-new fund has an empty ledger; the derived balance is zero.
        return fund.ToDto(0);
    }
}

public sealed record CloseFundCommand(Guid ActorId, Guid FundId) : IRequest<FundDto>;

internal sealed class CloseFundCommandHandler(
    IFundRepository funds,
    IFinancialTransactionRepository transactions,
    AuthorizationGuard guard,
    ILogger<CloseFundCommandHandler> logger)
    : IRequestHandler<CloseFundCommand, FundDto>
{
    public async Task<FundDto> Handle(CloseFundCommand cmd, CancellationToken ct)
    {
        var fund = await funds.GetByIdAsync(cmd.FundId, ct)
            ?? throw new FundNotFoundException(cmd.FundId);

        await FinanceAuthorization.RequireForFundAsync(guard, cmd.ActorId, FinancePermissions.FundManage, fund, ct);

        fund.Close();
        await funds.UpdateAsync(fund, ct);
        logger.FundClosed(fund.Id);

        var balance = await transactions.GetApprovedBalanceMinorUnitsAsync(fund.Id, ct);
        return fund.ToDto(balance);
    }
}

public sealed record RecordTransactionCommand(
    Guid ActorId,
    Guid FundId,
    string Type,
    string Currency,
    long MinorUnits,
    string? Description,
    Guid? TransferDestinationFundId) : IRequest<FinancialTransactionDto>;

internal sealed class RecordTransactionCommandHandler(
    IFundRepository funds,
    AuthorizationGuard guard,
    IMediator mediator,
    ILogger<RecordTransactionCommandHandler> logger)
    : IRequestHandler<RecordTransactionCommand, FinancialTransactionDto>
{
    public async Task<FinancialTransactionDto> Handle(RecordTransactionCommand cmd, CancellationToken ct)
    {
        var fund = await funds.GetByIdAsync(cmd.FundId, ct)
            ?? throw new FundNotFoundException(cmd.FundId);

        await FinanceAuthorization.RequireForFundAsync(
            guard, cmd.ActorId, FinancePermissions.TransactionRecord, fund, ct);

        var type = FinancialTransactionType.FromName(cmd.Type);
        var amount = Money.Create(cmd.Currency, cmd.MinorUnits);

        var transaction = fund.RecordTransaction(
            Guid.NewGuid(),
            amount,
            type,
            cmd.Description,
            cmd.TransferDestinationFundId,
            cmd.ActorId,
            DateTime.UtcNow);

        // Outbox: publish the recorded domain event before the single
        // transaction commits so the forwarded integration event and the
        // ledger row are committed atomically (ADR-015, ratified).
        await FinanceDomainEventPublisher.PublishAsync(fund, mediator, ct);
        await funds.UpdateAsync(fund, ct);
        logger.TransactionRecorded(transaction.Id, fund.Id);

        return transaction.ToDto();
    }
}

public sealed record SubmitTransactionCommand(Guid ActorId, Guid TransactionId)
    : IRequest<FinancialTransactionDto>;

internal sealed class SubmitTransactionCommandHandler(
    IFinancialTransactionRepository transactions,
    AuthorizationGuard guard,
    ILogger<SubmitTransactionCommandHandler> logger)
    : IRequestHandler<SubmitTransactionCommand, FinancialTransactionDto>
{
    public async Task<FinancialTransactionDto> Handle(SubmitTransactionCommand cmd, CancellationToken ct)
    {
        var transaction = await transactions.GetByIdAsync(cmd.TransactionId, ct)
            ?? throw new FinancialTransactionNotFoundException(cmd.TransactionId);

        await FinanceAuthorization.RequireAtAsync(guard, cmd.ActorId,
            FinancePermissions.TransactionRecord, FinanceAuthorization.ForTransaction(transaction), ct);

        transaction.Submit(cmd.ActorId, DateTime.UtcNow);
        await transactions.UpdateAsync(transaction, ct);
        logger.TransactionSubmitted(transaction.Id);

        return transaction.ToDto();
    }
}

public sealed record ApproveTransactionCommand(Guid ActorId, Guid TransactionId)
    : IRequest<FinancialTransactionDto>;

internal sealed class ApproveTransactionCommandHandler(
    IFinancialTransactionRepository transactions,
    AuthorizationGuard guard,
    ILogger<ApproveTransactionCommandHandler> logger)
    : IRequestHandler<ApproveTransactionCommand, FinancialTransactionDto>
{
    public async Task<FinancialTransactionDto> Handle(ApproveTransactionCommand cmd, CancellationToken ct)
    {
        var transaction = await transactions.GetByIdAsync(cmd.TransactionId, ct)
            ?? throw new FinancialTransactionNotFoundException(cmd.TransactionId);

        await FinanceAuthorization.RequireAtAsync(guard, cmd.ActorId,
            FinancePermissions.TransactionApprove, FinanceAuthorization.ForTransaction(transaction), ct);

        transaction.Approve(cmd.ActorId, DateTime.UtcNow);
        await transactions.UpdateAsync(transaction, ct);
        logger.TransactionApproved(transaction.Id);

        return transaction.ToDto();
    }
}

public sealed record RejectTransactionCommand(
    Guid ActorId,
    Guid TransactionId,
    string? Reason) : IRequest<FinancialTransactionDto>;

internal sealed class RejectTransactionCommandHandler(
    IFinancialTransactionRepository transactions,
    AuthorizationGuard guard,
    ILogger<RejectTransactionCommandHandler> logger)
    : IRequestHandler<RejectTransactionCommand, FinancialTransactionDto>
{
    public async Task<FinancialTransactionDto> Handle(RejectTransactionCommand cmd, CancellationToken ct)
    {
        var transaction = await transactions.GetByIdAsync(cmd.TransactionId, ct)
            ?? throw new FinancialTransactionNotFoundException(cmd.TransactionId);

        await FinanceAuthorization.RequireAtAsync(guard, cmd.ActorId,
            FinancePermissions.TransactionApprove, FinanceAuthorization.ForTransaction(transaction), ct);

        transaction.Reject(cmd.ActorId, DateTime.UtcNow, cmd.Reason);
        await transactions.UpdateAsync(transaction, ct);
        logger.TransactionRejected(transaction.Id);

        return transaction.ToDto();
    }
}