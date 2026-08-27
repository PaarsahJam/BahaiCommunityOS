using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Finance.API.Logging;
using CommunityOS.Finance.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace CommunityOS.Finance.API.Middleware;

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
            FundNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            FinancialTransactionNotFoundException => (StatusCodes.Status404NotFound, ex.Message),

            AuthorizationForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication required."),

            InvalidFundTransitionException => (StatusCodes.Status409Conflict, ex.Message),
            ClosedFundTransactionException => (StatusCodes.Status409Conflict, ex.Message),
            InvalidTransactionTransitionException => (StatusCodes.Status409Conflict, ex.Message),
            TransactionApprovalConflictException => (StatusCodes.Status409Conflict, ex.Message),

            InvalidFundScopeException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidTransactionTypeException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidCurrencyCodeException => (StatusCodes.Status400BadRequest, ex.Message),
            TransactionCurrencyMismatchException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidFinancialAmountException => (StatusCodes.Status400BadRequest, ex.Message),
            TransferReferenceRequiredException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidTransferReferenceException => (StatusCodes.Status400BadRequest, ex.Message),
            InvalidRejectionReasonException => (StatusCodes.Status400BadRequest, ex.Message),

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