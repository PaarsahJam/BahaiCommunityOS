namespace CommunityOS.Audit.Domain.Exceptions;

/// <summary>
/// Raised when a journal entry does not exist or must not be disclosed to the
/// caller (anti-enumeration: missing and unauthorized read identically
/// surface as 404; ADR-027 decision 13). Hold lookups reuse the same 404
/// contract.
/// </summary>
public sealed class AuditEntryNotFoundException : Exception
{
    public AuditEntryNotFoundException()
        : base("The requested audit resource was not found.")
    {
    }
}

/// <summary>
/// Raised for conflict states on administrative operations: placing a second
/// active hold on an entry, releasing an already-released hold (ADR-027
/// decision 13 error contract, HTTP 409).
/// </summary>
public sealed class AuditConflictException : Exception
{
    public AuditConflictException(string message)
        : base(message)
    {
    }
}
