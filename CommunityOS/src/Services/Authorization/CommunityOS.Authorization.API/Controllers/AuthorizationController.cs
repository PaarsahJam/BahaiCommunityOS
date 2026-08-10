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
public sealed class AuthorizationController(IMediator mediator) : ControllerBase
{
    [HttpPost("check")]
    [ProducesResponseType<AuthorizationCheckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthorizationCheckDto>> Check(
        CheckRequest request, CancellationToken ct)
    {
        var actorId = User.GetSubjectId();
        var result = await mediator.Send(new CheckPermissionCommand(
            actorId,
            request.SubjectId ?? actorId,
            request.Permission,
            request.OrganizationUnitId,
            request.ResourceType,
            request.ResourceId,
            request.CheckKey,
            request.Attributes), ct);
        return Ok(result);
    }

    [HttpPost("batch-check")]
    [ProducesResponseType<BatchAuthorizationCheckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchAuthorizationCheckDto>> BatchCheck(
        BatchCheckRequest request, CancellationToken ct)
    {
        var actorId = User.GetSubjectId();
        var items = request.Requests
            .Select(r => new CheckRequestItem(
                r.SubjectId ?? actorId,
                r.Permission,
                r.OrganizationUnitId,
                r.ResourceType,
                r.ResourceId,
                r.CheckKey,
                r.Attributes))
            .ToList();

        var result = await mediator.Send(
            new BatchCheckPermissionCommand(actorId, items), ct);
        return Ok(result);
    }

    [HttpPost("relationships/write")]
    [ProducesResponseType<RelationshipDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RelationshipDto>> WriteRelationship(
        RelationshipWriteRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new WriteRelationshipCommand(
            User.GetSubjectId(),
            request.SubjectId,
            request.Relation,
            request.ObjectType,
            request.ObjectId,
            request.Permissions,
            request.OrganizationUnitId), ct);
        return Ok(result);
    }

    [HttpPost("relationships/read")]
    [ProducesResponseType<IReadOnlyList<RelationshipDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RelationshipDto>>> ReadRelationships(
        RelationshipsReadRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ReadRelationshipsQuery(
            User.GetSubjectId(),
            request.SubjectId,
            request.Relation,
            request.ObjectType,
            request.ObjectId), ct);
        return Ok(result);
    }

    [HttpPost("delegations/grant")]
    [ProducesResponseType<DelegationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DelegationDto>> GrantDelegation(
        DelegationGrantRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new GrantDelegationCommand(
            User.GetSubjectId(),
            request.DelegateId,
            request.Permissions,
            request.ScopeType,
            request.ScopeId,
            request.ResourceType,
            request.StartsOn,
            request.ExpiresOn,
            request.Reason), ct);
        return Ok(result);
    }

    [HttpPost("delegations/revoke")]
    [ProducesResponseType<DelegationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DelegationDto>> RevokeDelegation(
        DelegationRevokeRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RevokeDelegationCommand(
            User.GetSubjectId(), request.DelegationId, request.Reason), ct);
        return Ok(result);
    }

    [HttpPost("breakglass/request")]
    [ProducesResponseType<BreakGlassRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BreakGlassRequestDto>> RequestBreakGlass(
        BreakGlassRequestRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RequestBreakGlassCommand(
            User.GetSubjectId(),
            request.Permissions,
            request.ScopeType,
            request.ScopeId,
            request.ResourceType,
            request.Reason,
            request.RequestedDurationMinutes), ct);
        return Ok(result);
    }

    [HttpPost("breakglass/approve")]
    [ProducesResponseType<BreakGlassRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BreakGlassRequestDto>> ApproveBreakGlass(
        BreakGlassApprovalRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ApproveBreakGlassCommand(User.GetSubjectId(), request.RequestId), ct);
        return Ok(result);
    }

    [HttpPost("breakglass/reject")]
    [ProducesResponseType<BreakGlassRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BreakGlassRequestDto>> RejectBreakGlass(
        BreakGlassRejectionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RejectBreakGlassCommand(
            User.GetSubjectId(), request.RequestId, request.Reason), ct);
        return Ok(result);
    }

    [HttpPost("breakglass/revoke")]
    [ProducesResponseType<BreakGlassRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BreakGlassRequestDto>> RevokeBreakGlass(
        BreakGlassRevocationRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RevokeBreakGlassCommand(
            User.GetSubjectId(), request.RequestId, request.Reason), ct);
        return Ok(result);
    }

    [HttpGet("breakglass")]
    [ProducesResponseType<IReadOnlyList<BreakGlassRequestDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BreakGlassRequestDto>>> ListBreakGlass(
        [FromQuery] string? state, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListBreakGlassRequestsQuery(User.GetSubjectId(), state), ct);
        return Ok(result);
    }
}

public sealed record CheckRequest(
    Guid? SubjectId,
    string Permission,
    Guid? OrganizationUnitId,
    string? ResourceType,
    Guid? ResourceId,
    string? CheckKey,
    IReadOnlyDictionary<string, string>? Attributes = null);

public sealed record BatchCheckRequest(IReadOnlyList<CheckRequest> Requests);

public sealed record RelationshipWriteRequest(
    Guid SubjectId,
    string Relation,
    string ObjectType,
    Guid ObjectId,
    IReadOnlyList<string>? Permissions,
    Guid? OrganizationUnitId);

public sealed record RelationshipsReadRequest(
    Guid? SubjectId,
    string? Relation,
    string? ObjectType,
    Guid? ObjectId);

public sealed record DelegationGrantRequest(
    Guid DelegateId,
    IReadOnlyList<string> Permissions,
    string ScopeType,
    Guid? ScopeId,
    string? ResourceType,
    DateTime? StartsOn,
    DateTime? ExpiresOn,
    string? Reason);

public sealed record DelegationRevokeRequest(Guid DelegationId, string? Reason);

public sealed record BreakGlassRequestRequest(
    IReadOnlyList<string> Permissions,
    string ScopeType,
    Guid? ScopeId,
    string? ResourceType,
    string Reason,
    int RequestedDurationMinutes);

public sealed record BreakGlassApprovalRequest(Guid RequestId);

public sealed record BreakGlassRejectionRequest(Guid RequestId, string? Reason);

public sealed record BreakGlassRevocationRequest(Guid RequestId, string? Reason);
