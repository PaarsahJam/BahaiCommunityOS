using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Community.Application.Logging;
using CommunityOS.Community.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace CommunityOS.Community.API.Middleware;

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
            CommunityNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            LocalUnitNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            PersonNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            PersonNotLinkedToAccountException => (StatusCodes.Status404NotFound, ex.Message),
            HouseholdNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            FamilyRelationshipNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            MembershipNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            ActivityNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            CommunityEventNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            MeetingNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            MeetingParticipantNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            MeetingAgendaItemNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            MeetingActionNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            ParticipationNotFoundException => (StatusCodes.Status404NotFound, ex.Message),

            AuthorizationForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication required."),

            DuplicateCommunityNameException => (StatusCodes.Status409Conflict, ex.Message),
            PersonAlreadyLinkedException => (StatusCodes.Status409Conflict, ex.Message),
            PersonAlreadyDeactivatedException => (StatusCodes.Status409Conflict, ex.Message),
            DuplicateContactMethodException => (StatusCodes.Status409Conflict, ex.Message),
            DuplicateHouseholdMemberException => (StatusCodes.Status409Conflict, ex.Message),
            DuplicateFamilyRelationshipException => (StatusCodes.Status409Conflict, ex.Message),
            FamilyRelationshipAlreadyEndedException => (StatusCodes.Status409Conflict, ex.Message),
            DuplicateMembershipException => (StatusCodes.Status409Conflict, ex.Message),
            MembershipStatusUnchangedException => (StatusCodes.Status409Conflict, ex.Message),
            ActivityAlreadyCancelledException => (StatusCodes.Status409Conflict, ex.Message),
            CommunityEventAlreadyCancelledException => (StatusCodes.Status409Conflict, ex.Message),
            MeetingAlreadyCancelledException => (StatusCodes.Status409Conflict, ex.Message),
            DuplicateMeetingParticipantException => (StatusCodes.Status409Conflict, ex.Message),
            DuplicateParticipationException => (StatusCodes.Status409Conflict, ex.Message),
            ParticipationAlreadyCancelledException => (StatusCodes.Status409Conflict, ex.Message),

            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed."),
            InvalidContactMethodException => (StatusCodes.Status400BadRequest, ex.Message),
            MultiplePreferredContactException => (StatusCodes.Status400BadRequest, ex.Message),
            SelfFamilyRelationshipException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidEffectivePeriodException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidTimeRangeException => (StatusCodes.Status400BadRequest, ex.Message),
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