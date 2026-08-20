using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Notifications.API.Logging;
using CommunityOS.Notifications.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace CommunityOS.Notifications.API.Middleware;

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
            NotificationNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            NotificationTypeNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            NotificationPreferenceNotFoundException => (StatusCodes.Status404NotFound, ex.Message),

            AuthorizationForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication required."),

            InvalidNotificationTransitionException => (StatusCodes.Status409Conflict, ex.Message),
            DuplicateNotificationTypeException => (StatusCodes.Status409Conflict, ex.Message),
            NotificationTypeInUseException => (StatusCodes.Status409Conflict, ex.Message),
            RetiredNotificationTypeUpdateException => (StatusCodes.Status409Conflict, ex.Message),

            InvalidNotificationTypeReferenceException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidNotificationChannelException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidMemberReferenceException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidScopeReferenceException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidNotificationException => (StatusCodes.Status400BadRequest, ex.Message),

            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed."),
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