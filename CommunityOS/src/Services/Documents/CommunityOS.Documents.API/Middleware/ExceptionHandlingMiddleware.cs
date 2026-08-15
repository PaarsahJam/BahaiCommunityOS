using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Documents.API.Logging;
using CommunityOS.Documents.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace CommunityOS.Documents.API.Middleware;

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
            DocumentNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            DocumentVersionNotFoundException => (StatusCodes.Status404NotFound, ex.Message),

            AuthorizationForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            ContentNotDownloadableException => (StatusCodes.Status403Forbidden, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication required."),

            HeldDocumentDeactivationException => (StatusCodes.Status409Conflict, ex.Message),

            InvalidDocumentTransitionException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidDocumentScopeException => (StatusCodes.Status400BadRequest, ex.Message),
            DuplicateDocumentReferenceException => (StatusCodes.Status400BadRequest, ex.Message),

            DocumentContentTooLargeException => (StatusCodes.Status413PayloadTooLarge, ex.Message),
            UnsupportedDocumentMimeTypeException => (StatusCodes.Status415UnsupportedMediaType, ex.Message),
            DocumentIntegrityViolationException => (StatusCodes.Status500InternalServerError, ex.Message),

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