using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.Events;
using CommunityOS.Finance.Domain.Exceptions;
using CommunityOS.Finance.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Finance.Domain.Aggregates;

/// <summary>
/// A fund: the ledger that holds money for one organization unit
/// (ADR-032). A fund owns its currency, lifecycle and transaction entries;
/// its balance is always derived from <c>Approved</c> entries and never
/// persisted.
/// </summary>
public sealed class Fund : AggregateRoot<Guid>
{
    private readonly List<FinancialTransaction> _transactions = [];

    private Fund()
        : base(Guid.Empty) { }

    private Fund(Guid id,
                 Guid organizationUnitId,
                 string organizationUnitDisplayName,
                 string name,
                 string currency)
        : base(id)
    {
        OrganizationUnitId = organizationUnitId;
        OrganizationUnitDisplayName = organizationUnitDisplayName;
        Name = name;
        Currency = currency;
        Status = FundStatus.Active;
        Revision = 1;
        var now = DateTime.UtcNow;
        CreatedOn = now;
        UpdatedOn = now;
    }

    /// <summary>Owning organization-unit scope (cross-context reference, ADR-016).</summary>
    public Guid OrganizationUnitId { get; private set; }

    /// <summary>Denormalized snapshot of the owning unit's name, kept current by the Organization consumer.</summary>
    public string OrganizationUnitDisplayName { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>Uppercase ISO-4217 code; every entry in this fund must match it.</summary>
    public string Currency { get; private set; } = null!;

    public FundStatus Status { get; private set; } = null!;

    /// <summary>Optimistic concurrency token; bumped on every mutation.</summary>
    public int Revision { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public IReadOnlyCollection<FinancialTransaction> Transactions => _transactions;

    public bool CanAcceptNewTransactions =>
        Status == FundStatus.Active;

    public static Fund Create(
        Guid id,
        Guid organizationUnitId,
        string organizationUnitDisplayName,
        string name,
        string currency)
    {
        if (!FinanceConstants.IsValidCurrencyCode(currency))
            throw new InvalidCurrencyCodeException(currency);

        return new Fund(id, organizationUnitId, organizationUnitDisplayName, name, currency.ToUpperInvariant());
    }

    /// <summary>
    /// Appends a new ledger entry (ADR-032). The direction is derived from the
    /// transaction type (never client-supplied), the currency must equal the
    /// fund's, and the status is initially <c>Recorded</c>. The entry inherits
    /// the fund's organizational scope for later authorization checks. Raising
    /// the <c>FinanceTransactionRecordedEvent</c> is the only domain event of
    /// the ratified gate: approve/reject and fund lifecycle changes raise
    /// nothing.
    /// </summary>
    public FinancialTransaction RecordTransaction(
        Guid transactionId,
        Money amount,
        FinancialTransactionType type,
        string? description,
        Guid? transferDestinationFundId,
        Guid recordedBy,
        DateTime occurredOn)
    {
        if (!CanAcceptNewTransactions)
            throw new ClosedFundTransactionException(Id);

        if (amount.Currency != Currency)
            throw new TransactionCurrencyMismatchException(Id, Currency, amount.Currency);

        var transaction = FinancialTransaction.Create(
            transactionId,
            Id,
            amount,
            type,
            description,
            transferDestinationFundId,
            recordedBy,
            occurredOn);

        transaction.AssignOrganizationUnitScope(OrganizationUnitId, OrganizationUnitDisplayName);
        _transactions.Add(transaction);
        RaiseDomainEvent(new FinanceTransactionRecordedEvent(
            transaction.Id,
            Id,
            type.Name,
            type.Direction.Name,
            transaction.Status.Name,
            recordedBy));
        Touch();

        return transaction;
    }

    /// <summary>
    /// Closes the fund. The ledger stays fully readable and preserves an audit
    /// trail; new entries are rejected while this fund's history remains
    /// intact. A closed fund cannot be reopened in the ratified first gate.
    /// </summary>
    public void Close()
    {
        if (Status == FundStatus.Closed)
            throw new InvalidFundTransitionException(Id, FundStatus.Closed.Name, FundStatus.Closed.Name);

        Status = FundStatus.Closed;
        Touch();
    }

    private void Touch()
    {
        Revision++;
        UpdatedOn = DateTime.UtcNow;
    }
}