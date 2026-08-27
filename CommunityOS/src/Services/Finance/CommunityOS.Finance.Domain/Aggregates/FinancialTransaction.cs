using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.Exceptions;
using CommunityOS.Finance.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Finance.Domain.Aggregates;

/// <summary>
/// A single append-only ledger entry (ADR-032). Once created, its content
/// (amount, currency, type, direction, description, transfer destination,
/// recorder, occurrence time) is immutable; only its status transitions
/// through <c>Recorded → Pending Approval → Approved | Rejected</c>. Only
/// <c>Approved</c> entries contribute to the derived fund balance.
/// </summary>
public sealed class FinancialTransaction : Entity<Guid>
{
    private FinancialTransaction()
        : base(Guid.Empty) { }

    private FinancialTransaction(
        Guid id,
        Guid fundId,
        Money amount,
        FinancialTransactionType type,
        string? description,
        Guid? transferDestinationFundId,
        Guid recordedBy,
        DateTime occurredOn)
        : base(id)
    {
        FundId = fundId;
        Amount = amount;
        Type = type;
        Description = description ?? string.Empty;
        TransferDestinationFundId = transferDestinationFundId;
        Status = FinancialTransactionStatus.Recorded;
        RecordedBy = recordedBy;
        OccurredOn = occurredOn;
    }

    public Guid FundId { get; private set; }

    public Guid? OrganizationUnitId { get; private set; }

    public string OrganizationUnitDisplayName { get; private set; } = string.Empty;

    public Money Amount { get; private set; } = null!;

    public FinancialTransactionType Type { get; private set; } = null!;

    /// <summary>
    /// Derived from <see cref="Type"/> at creation (ADR-032); never accepted
    /// from a client and never changed afterwards.
    /// </summary>
    public FinancialTransactionDirection Direction { get; private set; } = null!;

    public FinancialTransactionStatus Status { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    /// <summary>
    /// Required for a <c>Transfer</c> (where it is the inflow target fund),
    /// and forbidden for every other type. There is no cross-context foreign
    /// key: the value references a fund in this context or a deferred target
    /// context.
    /// </summary>
    public Guid? TransferDestinationFundId { get; private set; }

    public Guid RecordedBy { get; private set; }

    public DateTime OccurredOn { get; private set; }

    /// <summary>Recorder who moved the entry into <c>PendingApproval</c>.</summary>
    public Guid? SubmittedBy { get; private set; }

    public DateTime? SubmittedOn { get; private set; }

    /// <summary>Approver; must not equal <see cref="RecordedBy"/> (separation of duties).</summary>
    public Guid? ApprovedBy { get; private set; }

    public DateTime? ApprovedOn { get; private set; }

    /// <summary>Rejector; must not equal <see cref="RecordedBy"/> (separation of duties).</summary>
    public Guid? RejectedBy { get; private set; }

    public DateTime? RejectedOn { get; private set; }

    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Creates a <c>Recorded</c> entry for the owning fund. Applies the
    /// transfer reference rules (required for <c>Transfer</c>, distinct from
    /// the source fund, prohibited otherwise) and rejects a self-referencing
    /// destination before the row is ever persisted.
    /// </summary>
    public static FinancialTransaction Create(
        Guid id,
        Guid fundId,
        Money amount,
        FinancialTransactionType type,
        string? description,
        Guid? transferDestinationFundId,
        Guid recordedBy,
        DateTime occurredOn)
    {
        if (type == FinancialTransactionType.Transfer)
        {
            if (transferDestinationFundId is null || transferDestinationFundId == Guid.Empty)
                throw new TransferReferenceRequiredException(fundId);

            if (transferDestinationFundId == fundId)
                throw new InvalidTransferReferenceException(fundId, transferDestinationFundId.Value);
        }
        else if (transferDestinationFundId is not null)
        {
            throw new InvalidTransferReferenceException(fundId, transferDestinationFundId.Value);
        }

        var transaction = new FinancialTransaction(
            id,
            fundId,
            amount,
            type,
            description,
            transferDestinationFundId,
            recordedBy,
            occurredOn)
        {
            Direction = type.Direction,
        };

        return transaction;
    }

    /// <summary>Carries the owning fund's projected unit scope onto this entry.</summary>
    public void AssignOrganizationUnitScope(Guid organizationUnitId, string organizationUnitDisplayName)
    {
        OrganizationUnitId = organizationUnitId;
        OrganizationUnitDisplayName = organizationUnitDisplayName;
    }

    /// <summary>Moves <c>Recorded → PendingApproval</c>.</summary>
    public void Submit(Guid actorId, DateTime submittedOn)
    {
        if (Status != FinancialTransactionStatus.Recorded)
            throw new InvalidTransactionTransitionException(Id, Status.Name, FinancialTransactionStatus.PendingApproval.Name);

        Status = FinancialTransactionStatus.PendingApproval;
        SubmittedBy = actorId;
        SubmittedOn = submittedOn;
    }

    /// <summary>Moves <c>PendingApproval → Approved</c> (separate actor required).</summary>
    public void Approve(Guid actorId, DateTime approvedOn)
    {
        if (Status != FinancialTransactionStatus.PendingApproval)
            throw new InvalidTransactionTransitionException(Id, Status.Name, FinancialTransactionStatus.Approved.Name);

        if (actorId == RecordedBy)
            throw new TransactionApprovalConflictException(Id);

        Status = FinancialTransactionStatus.Approved;
        ApprovedBy = actorId;
        ApprovedOn = approvedOn;
    }

    /// <summary>Moves <c>PendingApproval → Rejected</c> (separate actor required, terminal).</summary>
    public void Reject(Guid actorId, DateTime rejectedOn, string? reason)
    {
        if (Status != FinancialTransactionStatus.PendingApproval)
            throw new InvalidTransactionTransitionException(Id, Status.Name, FinancialTransactionStatus.Rejected.Name);

        if (actorId == RecordedBy)
            throw new TransactionApprovalConflictException(Id);

        reason ??= string.Empty;
        if (reason.Length > 500)
            throw new InvalidRejectionReasonException();

        Status = FinancialTransactionStatus.Rejected;
        RejectedBy = actorId;
        RejectedOn = rejectedOn;
        RejectionReason = reason;
    }
}