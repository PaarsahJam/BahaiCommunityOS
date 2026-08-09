using Asp.Versioning;
using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Identity.API.Controllers;

/// <summary>
/// OAuth 2.1 / OpenID Connect endpoints for public (native) clients.
/// <c>authorize</c> performs an interactive login and issues a single-use
/// authorization code bound to a PKCE challenge; <c>token</c> exchanges the
/// code for tokens (verifying the PKCE verifier) or rotates a refresh token.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/connect")]
[ApiVersion(1.0)]
[AllowAnonymous]
public sealed class ConnectController(IMediator mediator) : ControllerBase
{
    [HttpPost("authorize")]
    [ProducesResponseType<AuthorizationCodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthorizationCodeDto>> Authorize(
        AuthorizeRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateAuthorizationCodeCommand(
            request.Email,
            request.Password,
            request.ClientId,
            request.RedirectUri,
            request.CodeChallenge,
            request.CodeChallengeMethod,
            request.Scope,
            request.MfaCode,
            request.State,
            request.Nonce,
            request.IpAddress ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString(),
            request.UserAgent), ct);

        return Ok(result);
    }

    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType<TokenResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TokenResponseDto>> Token(
        TokenRequest request, CancellationToken ct)
    {
        var ipAddress = request.IpAddress
            ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString();

        return request.GrantType switch
        {
            "authorization_code" => Ok(await mediator.Send(
                new ExchangeAuthorizationCodeCommand(
                    request.Code!, request.CodeVerifier!, request.ClientId!, request.RedirectUri!), ct)),

            "refresh_token" => Map(await mediator.Send(
                new RefreshSessionCommand(request.RefreshToken!, ipAddress, request.ClientId), ct)),

            _ => throw new UnsupportedGrantTypeException(request.GrantType)
        };
    }

    private static TokenResponseDto Map(TokenDto token) =>
        new(
            token.AccessToken,
            "Bearer",
            Math.Max(1, (int)Math.Ceiling((token.ExpiresAt - DateTime.UtcNow).TotalSeconds)),
            token.RefreshToken,
            Scope: null,
            IdToken: null);
}

public sealed record AuthorizeRequest(
    string Email,
    string Password,
    string ClientId,
    string RedirectUri,
    string CodeChallenge,
    string CodeChallengeMethod,
    string Scope,
    string? MfaCode = null,
    string? State = null,
    string? Nonce = null,
    string? IpAddress = null,
    string? UserAgent = null);

public sealed record TokenRequest(
    [FromForm(Name = "grant_type")] string GrantType,
    [FromForm(Name = "client_id")] string? ClientId,
    [FromForm(Name = "code")] string? Code,
    [FromForm(Name = "code_verifier")] string? CodeVerifier,
    [FromForm(Name = "redirect_uri")] string? RedirectUri,
    [FromForm(Name = "refresh_token")] string? RefreshToken,
    [FromForm] string? IpAddress = null);
