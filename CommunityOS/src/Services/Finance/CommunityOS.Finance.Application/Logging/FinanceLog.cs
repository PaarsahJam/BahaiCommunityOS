using Microsoft.Extensions.Logging;

namespace CommunityOS.Finance.Application.Logging;

/// <summary>
/// Structured, parameterized logging for the Finance service (ADR-032). Log
/// messages never include amounts, currencies, transaction types, description
/// text, donor attribution or transfer destinations — only stable identifiers
/// and lifecycle metadata, so logs can never leak financial data.
/// </summary>
public static partial class FinanceLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Ledger entry recorded: transaction {TransactionId} on fund {FundId}.")]
    public static partial void TransactionRecorded(this ILogger logger, Guid transactionId, Guid fundId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Ledger entry {TransactionId} submitted for approval.")]
    public static partial void TransactionSubmitted(this ILogger logger, Guid transactionId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Ledger entry {TransactionId} approved.")]
    public static partial void TransactionApproved(this ILogger logger, Guid transactionId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Ledger entry {TransactionId} rejected.")]
    public static partial void TransactionRejected(this ILogger logger, Guid transactionId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Fund {FundId} closed.")]
    public static partial void FundClosed(this ILogger logger, Guid fundId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Unhandled exception: {Message}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string message);
}