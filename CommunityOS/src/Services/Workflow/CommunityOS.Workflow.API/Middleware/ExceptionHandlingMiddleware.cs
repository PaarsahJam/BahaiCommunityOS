using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Workflow.API.Logging;
using CommunityOS.Workflow.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace CommunityOS.Workflow.API.Middleware;

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
            WorkflowTaskNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            TaskDefinitionNotFoundException => (StatusCodes.Status404NotFound, ex.Message),

            AuthorizationForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            TaskNotAssignableToActorException => (StatusCodes.Status403Forbidden, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication required."),

            InvalidWorkflowTaskTransitionException => (StatusCodes.Status409Conflict, ex.Message),
            DuplicateTaskDefinitionException => (StatusCodes.Status409Conflict, ex.Message),
            TaskDefinitionInUseException => (StatusCodes.Status409Conflict, ex.Message),
            RetiredTaskDefinitionUpdateException => (StatusCodes.Status409Conflict, ex.Message),

            InvalidWorkflowOutcomeException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidTaskDefinitionReferenceException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidTaskDomainTypeException => (StatusCodes.Status400BadRequest, ex.Message),
            TaskDomainEntityIdRequiredException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidTaskScopeException => (StatusCodes.Status400BadRequest, ex.Message),

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