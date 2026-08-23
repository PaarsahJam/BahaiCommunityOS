using CommunityOS.Audit.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace CommunityOS.Audit.API.Middleware;

internal sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            logger.UnhandledException(ex, ex.Message);
            await WriteErrorAsync(ctx, ex);
        }
    }

    private static Task WriteErrorAsync(HttpContext ctx, Exception ex)
    {
        var (status, title) = ex switch
        {
            AuthorizationForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication required."),

            // Ratified error contract (docs/api/audit.md): validation failures
            // are 400; missing AND unauthorized single reads are uniformly 404
            // (no enumeration oracle); hold conflicts are 409.
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed."),
            ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
            AuditEntryNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            AuditConflictException => (StatusCodes.Status409Conflict, ex.Message),

            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
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

internal static partial class AuditApiLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string message);
}
