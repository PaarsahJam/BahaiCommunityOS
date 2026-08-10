using CommunityOS.Authorization.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace CommunityOS.Authorization.API.Middleware;

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
            RoleNotFoundException                 => (StatusCodes.Status404NotFound, ex.Message),
            RoleAssignmentNotFoundException       => (StatusCodes.Status404NotFound, ex.Message),
            RelationshipNotFoundException         => (StatusCodes.Status404NotFound, ex.Message),
            DelegationNotFoundException           => (StatusCodes.Status404NotFound, ex.Message),
            BreakGlassRequestNotFoundException    => (StatusCodes.Status404NotFound, ex.Message),
            RoleCodeAlreadyExistsException        => (StatusCodes.Status409Conflict,  ex.Message),
            RoleAssignmentAlreadyRevokedException => (StatusCodes.Status409Conflict,  ex.Message),
            DelegationAlreadyRevokedException     => (StatusCodes.Status409Conflict,  ex.Message),
            BreakGlassAlreadyProcessedException   => (StatusCodes.Status409Conflict,  ex.Message),
            AuthorizationForbiddenException       => (StatusCodes.Status403Forbidden, ex.Message),
            UnauthorizedAccessException           => (StatusCodes.Status401Unauthorized, ex.Message),
            InvalidPermissionException            => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidRelationException              => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidEffectiveRangeException        => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidDelegationPeriodException      => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidDelegationScopeException       => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidDelegationRequestException     => (StatusCodes.Status400BadRequest, ex.Message),
            SelfDelegationException               => (StatusCodes.Status400BadRequest, ex.Message),
            SelfApprovalForbiddenException        => (StatusCodes.Status400BadRequest, ex.Message),
            BreakGlassGlobalScopeForbiddenException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidBreakGlassRequestException     => (StatusCodes.Status400BadRequest, ex.Message),
            ValidationException                   => (StatusCodes.Status400BadRequest, "Validation failed."),
            _                                     => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
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
