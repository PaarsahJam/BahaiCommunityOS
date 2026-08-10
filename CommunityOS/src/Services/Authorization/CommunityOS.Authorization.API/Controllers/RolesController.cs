using Asp.Versioning;
using CommunityOS.Authorization.API.Extensions;
using CommunityOS.Authorization.Application.Commands;
using CommunityOS.Authorization.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Authorization.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/authz")]
[ApiVersion(1.0)]
[Authorize]
public sealed class RolesController(IMediator mediator) : ControllerBase
{
    [HttpGet("roles")]
    [ProducesResponseType<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> ListRoles(CancellationToken ct)
    {
        var result = await mediator.Send(new GetRolesQuery(User.GetSubjectId()), ct);
        return Ok(result);
    }

    [HttpPost("roles")]
    [ProducesResponseType<RoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RoleDto>> CreateRole(CreateRoleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateRoleCommand(
            User.GetSubjectId(),
            request.Code,
            request.DisplayName,
            request.Description,
            request.Permissions), ct);
        return Ok(result);
    }

    [HttpPut("roles/{roleId:guid}")]
    [ProducesResponseType<RoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> UpdateRole(
        Guid roleId, UpdateRoleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateRoleCommand(
            User.GetSubjectId(),
            roleId,
            request.DisplayName,
            request.Description,
            request.Permissions), ct);
        return Ok(result);
    }

    [HttpPost("roles/assign")]
    [ProducesResponseType<RoleAssignmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RoleAssignmentDto>> AssignRole(
        AssignRoleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AssignRoleCommand(
            User.GetSubjectId(),
            request.SubjectId,
            request.RoleId,
            request.ScopeType,
            request.ScopeId,
            request.ResourceType,
            request.EffectiveFrom,
            request.EffectiveUntil,
            request.Reason), ct);
        return Ok(result);
    }

    [HttpPost("roles/revoke")]
    [ProducesResponseType<RoleAssignmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RoleAssignmentDto>> RevokeRole(
        RevokeRoleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RevokeRoleCommand(
            User.GetSubjectId(), request.AssignmentId, request.Reason), ct);
        return Ok(result);
    }
}

public sealed record CreateRoleRequest(
    string Code,
    string DisplayName,
    string? Description,
    IReadOnlyList<string> Permissions);

public sealed record UpdateRoleRequest(
    string DisplayName,
    string? Description,
    IReadOnlyList<string> Permissions);

public sealed record AssignRoleRequest(
    Guid SubjectId,
    Guid RoleId,
    string ScopeType,
    Guid? ScopeId,
    string? ResourceType,
    DateTime? EffectiveFrom,
    DateTime? EffectiveUntil,
    string? Reason);

public sealed record RevokeRoleRequest(Guid AssignmentId, string? Reason);
