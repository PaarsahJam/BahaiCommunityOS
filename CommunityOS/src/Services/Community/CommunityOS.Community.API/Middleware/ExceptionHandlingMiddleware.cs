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
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await WriteErrorAsync(ctx, ex);
        }
    }

    private static Task WriteErrorAsync(HttpContext ctx, Exception ex)
    {
        var (status, title) = ex switch
        {
            CommunityNotFoundException       => (StatusCodes.Status404NotFound,            ex.Message),
            LocalUnitNotFoundException       => (StatusCodes.Status404NotFound,            ex.Message),
            DuplicateCommunityNameException  => (StatusCodes.Status409Conflict,            ex.Message),
            ValidationException              => (StatusCodes.Status400BadRequest,          "Validation failed."),
            _                                => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
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
