namespace CommunityOS.Finance.Domain.Exceptions;

/// <summary>404 — fund not found or not readable (indistinguishable).</summary>
public sealed class FundNotFoundException(Guid fundId)
    : Exception($"Fund '{fundId}' was not found.");

/// <summary>404 — ledger entry not found or not readable (indistinguishable).</summary>
public sealed class FinancialTransactionNotFoundException(Guid transactionId)
    : Exception($"Financial transaction '{transactionId}' was not found.");

/// <summary>400 — the referenced organization unit is not a known unit reference.</summary>
public sealed class InvalidFundScopeException(Guid organizationUnitId)
    : Exception($"Organization unit scope '{organizationUnitId}' is not a known unit reference.");

/// <summary>400 — a transaction type is not one of the ratified set.</summary>
public sealed class InvalidTransactionTypeException(string transactionType)
    : Exception($"Transaction type '{transactionType}' is not recognized.");

/// <summary>400 — the currency is not a valid uppercase ISO-4217 code.</summary>
public sealed class InvalidCurrencyCodeException(string currency)
    : Exception($"Currency '{currency}' is not a recognized ISO-4217 code (three uppercase letters).");

/// <summary>400 — a ledger entry currency must match its fund's currency.</summary>
public sealed class TransactionCurrencyMismatchException(Guid fundId, string fundCurrency, string currency)
    : Exception($"Transaction currency '{currency}' does not match fund '{fundId}' currency '{fundCurrency}'.");

/// <summary>400 — the amount must be a positive magnitude (direction carries the sign).</summary>
public sealed class InvalidFinancialAmountException(long minorUnits)
    : Exception($"Amount must be a positive magnitude (received {minorUnits} minor units).");

/// <summary>400 — a transfer requires a distinct destination fund reference.</summary>
public sealed class TransferReferenceRequiredException(Guid fundId)
    : Exception($"A transfer from fund '{fundId}' requires a destination fund reference.");

/// <summary>400 — the transfer destination is missing, equals the source fund, or is used by a non-transfer entry.</summary>
public sealed class InvalidTransferReferenceException(Guid fundId, Guid destinationFundId)
    : Exception($"Transfer destination '{destinationFundId}' is invalid for fund '{fundId}'.");

/// <summary>400 — a rejection must not carry a reason longer than the limit.</summary>
public sealed class InvalidRejectionReasonException()
    : Exception("Rejection reason exceeds the maximum length of 500 characters.");

/// <summary>409 — the fund lifecycle transition is not permitted.</summary>
public sealed class InvalidFundTransitionException(Guid fundId, string from, string to)
    : Exception($"Fund '{fundId}' cannot transition from '{from}' to '{to}'.");

/// <summary>409 — a closed fund rejects new ledger entries.</summary>
public sealed class ClosedFundTransactionException(Guid fundId)
    : Exception($"Fund '{fundId}' is closed and cannot accept new transactions.");

/// <summary>409 — the status transition is not permitted.</summary>
public sealed class InvalidTransactionTransitionException(Guid transactionId, string from, string to)
    : Exception($"Financial transaction '{transactionId}' cannot transition from '{from}' to '{to}'.");

/// <summary>409 — the recorder of an entry cannot approve or reject it (separation of duties).</summary>
public sealed class TransactionApprovalConflictException(Guid transactionId)
    : Exception($"Financial transaction '{transactionId}' cannot be approved or rejected by its recorder (separation of duties).");