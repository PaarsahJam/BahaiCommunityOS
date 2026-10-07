using CommunityOS.Identity.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CommunityOS.Identity.API.Middleware;

internal sealed partial class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    private static partial void LogUnhandledError(
        ILogger logger, string message);

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            LogUnhandledError(logger, ex.Message);
            await WriteErrorAsync(ctx, ex);
        }
    }

    private static Task WriteErrorAsync(HttpContext ctx, Exception ex)
    {
        var (status, title) = ex switch
        {
            UserAccountNotFoundException         => (StatusCodes.Status404NotFound,     ex.Message),
            UserAccountNotFoundByEmailException  => (StatusCodes.Status404NotFound,     ex.Message),
            SessionNotFoundException             => (StatusCodes.Status404NotFound,     ex.Message),
            MfaMethodNotFoundException           => (StatusCodes.Status404NotFound,     ex.Message),
            MfaLastVerifiedMethodException       => (StatusCodes.Status409Conflict,      ex.Message),
            DuplicateEmailException              => (StatusCodes.Status409Conflict,      ex.Message),
            DbUpdateConcurrencyException         => (StatusCodes.Status409Conflict,      "Concurrency conflict."),
            AccountNotVerifiedException          => (StatusCodes.Status403Forbidden,     ex.Message),
            AccountLockedException               => (StatusCodes.Status423Locked,        ex.Message),
            AccountDeactivatedException          => (StatusCodes.Status403Forbidden,     ex.Message),
            InvalidCredentialsException          => (StatusCodes.Status401Unauthorized,  ex.Message),
            InvalidMfaCodeException              => (StatusCodes.Status401Unauthorized,  ex.Message),
            MfaRequiredException                 => (StatusCodes.Status401Unauthorized,  ex.Message),
            InvalidSessionException              => (StatusCodes.Status401Unauthorized,  ex.Message),
            InvalidRefreshTokenException         => (StatusCodes.Status401Unauthorized,  ex.Message),
            RefreshTokenReuseDetectedException   => (StatusCodes.Status401Unauthorized,  ex.Message),
            InvalidRecoveryTokenException        => (StatusCodes.Status400BadRequest,    ex.Message),
            InvalidClientException               => (StatusCodes.Status401Unauthorized,  ex.Message),
            InvalidGrantException                => (StatusCodes.Status400BadRequest,    ex.Message),
            InvalidRedirectUriException          => (StatusCodes.Status400BadRequest,    ex.Message),
            UnauthorizedGrantException           => (StatusCodes.Status400BadRequest,    ex.Message),
            InvalidScopeException                => (StatusCodes.Status400BadRequest,    ex.Message),
            UnsupportedGrantTypeException        => (StatusCodes.Status400BadRequest,    ex.Message),
            UnauthorisedAccessException          => (StatusCodes.Status403Forbidden,     ex.Message),
            ValidationException                  => (StatusCodes.Status400BadRequest,    "Validation failed."),
            _                                    => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";

        var body = ex is ValidationException ve
            ? JsonSerializer.Serialize(new
            {
                title,
                status,
                errors = ve.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
            })
            : JsonSerializer.Serialize(new { title, status });

        return ctx.Response.WriteAsync(body);
    }
}
