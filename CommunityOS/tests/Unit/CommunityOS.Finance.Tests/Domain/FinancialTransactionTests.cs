using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Domain.Enumerations;
using CommunityOS.Finance.Domain.Exceptions;
using CommunityOS.Finance.Domain.ValueObjects;

namespace CommunityOS.Finance.Tests.Domain;

/// <summary>
/// Locks the ledger-entry invariants and state machine (ADR-032): immutable
/// content, derived direction, transfer reference rules, and the
/// <c>Recorded → Pending Approval → Approved | Rejected</c> transitions with
/// recorder separation of duties.
/// </summary>
public class FinancialTransactionTests
{
    private static readonly Guid FundId = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static Money Usd(long minorUnits) => Money.Create("USD", minorUnits);

    private static FinancialTransaction Entry(
        FinancialTransactionType type,
        Money? amount = null,
        Guid? destination = null) =>
        FinancialTransaction.Create(
            Guid.NewGuid(), FundId, amount ?? Usd(100), type, "purpose", destination, Actor, Now);

    [Fact]
    public void Create_records_a_contribution_as_inflow_in_recorded_state()
    {
        var tx = Entry(FinancialTransactionType.Contribution);

        tx.Direction.Should().Be(FinancialTransactionDirection.Inflow);
        tx.Status.Should().Be(FinancialTransactionStatus.Recorded);
        tx.FundId.Should().Be(FundId);
        tx.Amount.Should().Be(Usd(100));
        tx.Description.Should().Be("purpose");
        tx.TransferDestinationFundId.Should().BeNull();
    }

    [Fact]
    public void Create_derives_outflow_for_expense_reimbursement_and_disbursement()
    {
        Entry(FinancialTransactionType.Expense).Direction.Should()
            .Be(FinancialTransactionDirection.Outflow);
        Entry(FinancialTransactionType.Reimbursement).Direction.Should()
            .Be(FinancialTransactionDirection.Outflow);
        Entry(FinancialTransactionType.Disbursement).Direction.Should()
            .Be(FinancialTransactionDirection.Outflow);
    }

    [Fact]
    public void Create_rejects_a_transfer_without_a_destination()
    {
        var act = () => Entry(FinancialTransactionType.Transfer, destination: null);

        act.Should().Throw<TransferReferenceRequiredException>();
    }

    [Fact]
    public void Create_rejects_a_transfer_whose_destination_is_the_source_fund()
    {
        var act = () => FinancialTransaction.Create(
            Guid.NewGuid(), FundId, Usd(50), FinancialTransactionType.Transfer,
            null, FundId, Actor, Now);

        act.Should().Throw<InvalidTransferReferenceException>();
    }

    [Fact]
    public void Create_rejects_a_destination_reference_on_a_non_transfer_entry()
    {
        var act = () => Entry(FinancialTransactionType.Expense, destination: Guid.NewGuid());

        act.Should().Throw<InvalidTransferReferenceException>();
    }

    [Fact]
    public void Submit_moves_recorded_to_pending_approval_and_records_the_submitter()
    {
        var tx = Entry(FinancialTransactionType.Contribution);

        tx.Submit(Actor, Now);

        tx.Status.Should().Be(FinancialTransactionStatus.PendingApproval);
        tx.SubmittedBy.Should().Be(Actor);
        tx.SubmittedOn.Should().Be(Now);
    }

    [Fact]
    public void Submit_is_rejected_once_the_entry_has_moved_on()
    {
        var tx = Entry(FinancialTransactionType.Contribution);
        tx.Submit(Actor, Now);

        var act = () => tx.Submit(Actor, Now);

        act.Should().Throw<InvalidTransactionTransitionException>();
    }

    [Fact]
    public void Approve_moves_pending_approval_to_approved_and_records_the_approver()
    {
        var approver = Guid.NewGuid();
        var tx = Entry(FinancialTransactionType.Contribution);
        tx.Submit(Actor, Now);

        tx.Approve(approver, Now);

        tx.Status.Should().Be(FinancialTransactionStatus.Approved);
        tx.ApprovedBy.Should().Be(approver);
        tx.ApprovedOn.Should().Be(Now);
    }

    [Fact]
    public void Approve_cannot_be_performed_by_the_recorder()
    {
        var tx = Entry(FinancialTransactionType.Contribution);
        tx.Submit(Actor, Now);

        var act = () => tx.Approve(Actor, Now);

        act.Should().Throw<TransactionApprovalConflictException>();
    }

    [Fact]
    public void Approve_is_rejected_until_the_entry_has_been_submitted()
    {
        var tx = Entry(FinancialTransactionType.Contribution);

        var act = () => tx.Approve(Guid.NewGuid(), Now);

        act.Should().Throw<InvalidTransactionTransitionException>();
    }

    [Fact]
    public void Reject_moves_pending_approval_to_rejected_terminal_state()
    {
        var rejector = Guid.NewGuid();
        var tx = Entry(FinancialTransactionType.Contribution);
        tx.Submit(Actor, Now);

        tx.Reject(rejector, Now, "duplicate entry");

        tx.Status.Should().Be(FinancialTransactionStatus.Rejected);
        tx.RejectedBy.Should().Be(rejector);
        tx.RejectionReason.Should().Be("duplicate entry");

        var act = () => tx.Approve(rejector, Now);
        act.Should().Throw<InvalidTransactionTransitionException>();
    }

    [Fact]
    public void Reject_cannot_be_performed_by_the_recorder()
    {
        var tx = Entry(FinancialTransactionType.Contribution);
        tx.Submit(Actor, Now);

        var act = () => tx.Reject(Actor, Now, "self-rejection");

        act.Should().Throw<TransactionApprovalConflictException>();
    }

    [Fact]
    public void Reject_reason_is_limited_to_500_characters()
    {
        var tx = Entry(FinancialTransactionType.Contribution);
        tx.Submit(Actor, Now);

        var act = () => tx.Reject(Guid.NewGuid(), Now, new string('x', 501));

        act.Should().Throw<InvalidRejectionReasonException>();
    }
}