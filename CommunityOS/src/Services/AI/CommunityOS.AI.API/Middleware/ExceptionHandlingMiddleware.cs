using CommunityOS.AI.Domain.Exceptions;
using CommunityOS.Authorization.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace CommunityOS.AI.API.Middleware;

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
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication required."),
            AuthorizationForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            AiProviderDisabledException => (StatusCodes.Status503ServiceUnavailable, ex.Message),
            AiCapabilityNotSupportedException => (StatusCodes.Status400BadRequest, ex.Message),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed."),
            ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
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

internal static partial class AiApiLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string message);
}
