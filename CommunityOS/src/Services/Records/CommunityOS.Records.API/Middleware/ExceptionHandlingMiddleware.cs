using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Records.API.Logging;
using CommunityOS.Records.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace CommunityOS.Records.API.Middleware;

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
            RecordNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            RecordVersionNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            RecordHoldNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            RecordEvidenceNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            RetentionScheduleNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            RecordCategoryNotFoundException => (StatusCodes.Status404NotFound, ex.Message),

            AuthorizationForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication required."),

            VerifiedRecordFieldUpdateException => (StatusCodes.Status409Conflict, ex.Message),
            CreatorVerificationConflictException => (StatusCodes.Status409Conflict, ex.Message),
            HoldReleaseByPlacerException => (StatusCodes.Status409Conflict, ex.Message),
            HeldRecordDeactivationException => (StatusCodes.Status409Conflict, ex.Message),
            DuplicateRecordEvidenceException => (StatusCodes.Status409Conflict, ex.Message),
            RetentionScheduleInUseException => (StatusCodes.Status409Conflict, ex.Message),
            DuplicateRecordCategoryException => (StatusCodes.Status409Conflict, ex.Message),
            DuplicateRetentionScheduleException => (StatusCodes.Status409Conflict, ex.Message),
            InvalidRecordTransitionException => (StatusCodes.Status409Conflict, ex.Message),

            InvalidRecordScopeException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidRetentionPeriodException => (StatusCodes.Status400BadRequest, ex.Message),
            CorrectionChangeReasonRequiredException => (StatusCodes.Status400BadRequest, ex.Message),
            AdminOverrideReasonRequiredException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidRecordSubjectTypeException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidRecordCategoryReferenceException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidRecordHoldTypeException => (StatusCodes.Status400BadRequest, ex.Message),

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