using Asp.Versioning;
using CommunityOS.Finance.API.Extensions;
using CommunityOS.Finance.Application.Commands;
using CommunityOS.Finance.Application.DTOs;
using CommunityOS.Finance.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Finance.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/transactions")]
[Authorize]
public sealed class TransactionsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet("{transactionId:guid}")]
    [ProducesResponseType<FinancialTransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid transactionId, CancellationToken ct)
    {
        var result = await sender.Send(new GetTransactionQuery(ActorId, transactionId), ct);
        return Ok(result);
    }

    [HttpPost("{transactionId:guid}/submit")]
    [ProducesResponseType<FinancialTransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid transactionId, CancellationToken ct)
    {
        var result = await sender.Send(new SubmitTransactionCommand(ActorId, transactionId), ct);
        return Ok(result);
    }

    [HttpPost("{transactionId:guid}/approve")]
    [ProducesResponseType<FinancialTransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid transactionId, CancellationToken ct)
    {
        var result = await sender.Send(new ApproveTransactionCommand(ActorId, transactionId), ct);
        return Ok(result);
    }

    [HttpPost("{transactionId:guid}/reject")]
    [ProducesResponseType<FinancialTransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid transactionId, RejectTransactionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RejectTransactionCommand(ActorId, transactionId, request.Reason), ct);
        return Ok(result);
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/funds/{fundId:guid}/transactions")]
[Authorize]
public sealed class FundTransactionsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FinancialTransactionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        Guid fundId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var result = await sender.Send(new ListTransactionsQuery(ActorId, fundId, status), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<FinancialTransactionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Record(Guid fundId, RecordTransactionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RecordTransactionCommand(
            ActorId,
            fundId,
            request.Type,
            request.Currency,
            request.MinorUnits,
            request.Description,
            request.TransferDestinationFundId), ct);
        return CreatedAtAction(
            nameof(TransactionsController.GetById),
            new { controller = "Transactions", transactionId = result.Id },
            result);
    }
}

public sealed record RecordTransactionRequest(
    string Type,
    string Currency,
    long MinorUnits,
    string? Description,
    Guid? TransferDestinationFundId);

public sealed record RejectTransactionRequest(string? Reason);