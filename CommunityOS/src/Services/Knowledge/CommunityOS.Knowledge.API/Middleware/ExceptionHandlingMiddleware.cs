using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Knowledge.Application.Logging;
using CommunityOS.Knowledge.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace CommunityOS.Knowledge.API.Middleware;

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
            WorkNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            EditionNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            PassageNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            QuestionNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            AnswerNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            DiscussionNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            AiSuggestionNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            CategoryNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            TopicNotFoundException => (StatusCodes.Status404NotFound, ex.Message),

            AuthorizationForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication required."),

            EditionAlreadyVerifiedException => (StatusCodes.Status409Conflict, ex.Message),
            QuestionAlreadyTerminalException => (StatusCodes.Status409Conflict, ex.Message),
            AlreadyAcceptedAnswerException => (StatusCodes.Status409Conflict, ex.Message),
            AiSuggestionAlreadyReviewedException => (StatusCodes.Status409Conflict, ex.Message),
            DiscussionAlreadyModeratedException => (StatusCodes.Status409Conflict, ex.Message),

            InvalidQuestionTransitionException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidMergeTargetException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidReferenceException => (StatusCodes.Status400BadRequest, ex.Message),
            AnswerNotOnQuestionException => (StatusCodes.Status400BadRequest, ex.Message),
            AiAssistedAnswerRequiresReviewException => (StatusCodes.Status400BadRequest, ex.Message),

            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        ctx.Response.StatusCode  = status;
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