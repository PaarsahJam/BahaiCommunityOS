using Asp.Versioning;
using CommunityOS.Identity.Application.Commands;
using CommunityOS.Identity.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Identity.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[ApiVersion(1.0)]
[AllowAnonymous]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<VerificationTokenDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VerificationTokenDto>> Register(
        RegisterRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RegisterCommand(request.Email, request.Password), ct);
        return CreatedAtAction(nameof(Register), result);
    }

    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(
        VerifyEmailRequest request, CancellationToken ct)
    {
        await mediator.Send(new VerifyEmailCommand(request.Token), ct);
        return NoContent();
    }

    [HttpPost("login")]
    [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponseDto>> Login(
        LoginRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new LoginCommand(
            request.Email,
            request.Password,
            request.MfaCode,
            request.DeviceName,
            request.Platform,
            request.UserAgent,
            request.IpAddress ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(result);
    }

    [HttpPost("refresh")]
    [ProducesResponseType<TokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenDto>> Refresh(
        RefreshRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RefreshSessionCommand(
            request.RefreshToken,
            request.IpAddress ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString()), ct);
        return Ok(result);
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType<RecoveryTokenDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecoveryTokenDto?>> ForgotPassword(
        ForgotPasswordRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RequestPasswordResetCommand(request.Email), ct);
        return Ok(result);
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(
            new ResetPasswordCommand(request.Token, request.NewPassword), ct);
        return NoContent();
    }
}

public sealed record RegisterRequest(string Email, string Password);

public sealed record VerifyEmailRequest(string Token);

public sealed record LoginRequest(
    string Email,
    string Password,
    string? MfaCode = null,
    string? DeviceName = null,
    string? Platform = null,
    string? UserAgent = null,
    string? IpAddress = null);

public sealed record RefreshRequest(string RefreshToken, string? IpAddress = null);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);
