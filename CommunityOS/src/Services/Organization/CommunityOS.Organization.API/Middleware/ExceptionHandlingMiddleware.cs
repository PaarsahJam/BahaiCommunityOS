using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Organization.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace CommunityOS.Organization.API.Middleware;

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
            OrganizationNotFoundException            => (StatusCodes.Status404NotFound, ex.Message),
            OrganizationUnitNotFoundException         => (StatusCodes.Status404NotFound, ex.Message),
            AppointmentNotFoundException             => (StatusCodes.Status404NotFound, ex.Message),
            CommitteeNotFoundException               => (StatusCodes.Status404NotFound, ex.Message),
            CommitteeMemberNotFoundException         => (StatusCodes.Status404NotFound, ex.Message),
            InstitutionNotFoundException             => (StatusCodes.Status404NotFound, ex.Message),
            DelegationFactNotFoundException          => (StatusCodes.Status404NotFound, ex.Message),

            OrganizationNameAlreadyExistsException   => (StatusCodes.Status409Conflict, ex.Message),
            OrganizationUnitNameAlreadyExistsException => (StatusCodes.Status409Conflict, ex.Message),
            OrganizationUnitAlreadyDeactivatedException => (StatusCodes.Status409Conflict, ex.Message),
            HierarchyCycleException                  => (StatusCodes.Status409Conflict, ex.Message),
            AppointmentAlreadyEndedException         => (StatusCodes.Status409Conflict, ex.Message),
            OverlappingAppointmentException          => (StatusCodes.Status409Conflict, ex.Message),
            DelegationFactAlreadyRevokedException    => (StatusCodes.Status409Conflict, ex.Message),

            SelfDelegationFactException              => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidEffectivePeriodException          => (StatusCodes.Status400BadRequest, ex.Message),

            AuthorizationForbiddenException          => (StatusCodes.Status403Forbidden, ex.Message),
            UnauthorizedAccessException              => (StatusCodes.Status401Unauthorized, ex.Message),
            ValidationException                      => (StatusCodes.Status400BadRequest, "Validation failed."),
            _                                        => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
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
