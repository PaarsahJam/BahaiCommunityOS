using Asp.Versioning;
using CommunityOS.Records.API.Extensions;
using CommunityOS.Records.Application.Commands;
using CommunityOS.Records.Application.DTOs;
using CommunityOS.Records.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Records.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/retention/schedules")]
[Authorize]
public sealed class RetentionController(ISender sender) : ControllerBase
{
    private Guid ActorId => User.GetSubjectId();

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RetentionScheduleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await sender.Send(new ListRetentionSchedulesQuery(ActorId), ct);
        return Ok(result);
    }

    [HttpGet("{code}")]
    [ProducesResponseType<RetentionScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
    {
        var result = await sender.Send(new GetRetentionScheduleQuery(ActorId, code), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<RetentionScheduleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateRetentionScheduleRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateRetentionScheduleCommand(
            ActorId,
            request.Code,
            request.DisplayName,
            request.Description,
            request.CategoryCodes,
            request.Rules.Select(r => new RetentionRuleInput(
                r.RetentionPeriod, r.StartTrigger, r.Disposition, r.Note, r.MaximumPeriod)).ToList()), ct);
        return CreatedAtAction(nameof(GetByCode), new { code = result.Code }, result);
    }

    [HttpPut("{code}")]
    [ProducesResponseType<RetentionScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string code, UpdateRetentionScheduleRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateRetentionScheduleCommand(
            ActorId,
            code,
            request.DisplayName,
            request.Description,
            request.CategoryCodes,
            request.Rules.Select(r => new RetentionRuleInput(
                r.RetentionPeriod, r.StartTrigger, r.Disposition, r.Note, r.MaximumPeriod)).ToList()), ct);
        return Ok(result);
    }

    [HttpDelete("{code}")]
    [ProducesResponseType<RetentionScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retire(string code, CancellationToken ct)
    {
        var result = await sender.Send(new RetireRetentionScheduleCommand(ActorId, code), ct);
        return Ok(result);
    }
}

public sealed record RetentionRuleRequest(
    string RetentionPeriod,
    string StartTrigger,
    string Disposition,
    string? Note,
    string? MaximumPeriod = null);

public sealed record CreateRetentionScheduleRequest(
    string Code,
    string DisplayName,
    string? Description,
    IReadOnlyList<string> CategoryCodes,
    IReadOnlyList<RetentionRuleRequest> Rules);

public sealed record UpdateRetentionScheduleRequest(
    string DisplayName,
    string? Description,
    IReadOnlyList<string> CategoryCodes,
    IReadOnlyList<RetentionRuleRequest> Rules);