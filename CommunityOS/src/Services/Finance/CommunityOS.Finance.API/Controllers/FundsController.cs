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
[Route("api/v{version:apiVersion}/funds")]
[Authorize]
public sealed class FundsController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FundSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? organizationUnitId,
        CancellationToken ct)
    {
        var result = await sender.Send(new ListFundsQuery(ActorId, organizationUnitId), ct);
        return Ok(result);
    }

    [HttpGet("{fundId:guid}")]
    [ProducesResponseType<FundDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid fundId, CancellationToken ct)
    {
        var result = await sender.Send(new GetFundQuery(ActorId, fundId), ct);
        return Ok(result);
    }

    [HttpGet("{fundId:guid}/balance")]
    [ProducesResponseType<FundBalanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance(Guid fundId, CancellationToken ct)
    {
        var result = await sender.Send(new GetFundBalanceQuery(ActorId, fundId), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<FundDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateFundRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateFundCommand(
            ActorId,
            request.OrganizationUnitId,
            request.Name,
            request.Currency), ct);
        return CreatedAtAction(nameof(GetById), new { fundId = result.Id }, result);
    }

    [HttpPost("{fundId:guid}/close")]
    [ProducesResponseType<FundDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(Guid fundId, CancellationToken ct)
    {
        var result = await sender.Send(new CloseFundCommand(ActorId, fundId), ct);
        return Ok(result);
    }
}

public sealed record CreateFundRequest(
    Guid OrganizationUnitId,
    string Name,
    string Currency);