using CommunityOS.Finance.Application.Commands;
using CommunityOS.Finance.Application.Validators;
using FluentValidation.TestHelper;

namespace CommunityOS.Finance.Tests.Validation;

/// <summary>
/// API validation contract (ADR-032 ratified boundary): commands are validated
/// through the FluentValidation pipeline before any authorization or
/// persistence; invalid currency codes, amounts, identifiers, descriptions and
/// transfer references are rejected with 400-class errors.
/// </summary>
public class FinanceCommandValidatorTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void CreateFund_requires_a_knowable_scope_name_and_uppercase_currency()
    {
        var validator = new CreateFundCommandValidator();

        validator.TestValidate(new CreateFundCommand(Guid.Empty, Guid.NewGuid(), "Sacred Fund", "USD"))
            .ShouldHaveValidationErrorFor(c => c.ActorId);
        validator.TestValidate(new CreateFundCommand(Actor, Guid.Empty, "Sacred Fund", "USD"))
            .ShouldHaveValidationErrorFor(c => c.OrganizationUnitId);
        validator.TestValidate(new CreateFundCommand(Actor, Guid.NewGuid(), "", "USD"))
            .ShouldHaveValidationErrorFor(c => c.Name);
        validator.TestValidate(new CreateFundCommand(Actor, Guid.NewGuid(), "Sacred Fund", "usd"))
            .ShouldHaveValidationErrorFor(c => c.Currency);
        validator.TestValidate(new CreateFundCommand(Actor, Guid.NewGuid(), "Sacred Fund", "US"))
            .ShouldHaveValidationErrorFor(c => c.Currency);
        validator.TestValidate(new CreateFundCommand(Actor, Guid.NewGuid(), new string('n', 201), "USD"))
            .ShouldHaveValidationErrorFor(c => c.Name);

        validator.TestValidate(new CreateFundCommand(Actor, Guid.NewGuid(), "Sacred Fund", "USD"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RecordTransaction_requires_type_currency_positive_amount_and_bounded_text()
    {
        var validator = new RecordTransactionCommandValidator();

        validator.TestValidate(new RecordTransactionCommand(Guid.Empty, Guid.NewGuid(), "contribution", "USD", 100, null, null))
            .ShouldHaveValidationErrorFor(c => c.ActorId);
        validator.TestValidate(new RecordTransactionCommand(Actor, Guid.Empty, "contribution", "USD", 100, null, null))
            .ShouldHaveValidationErrorFor(c => c.FundId);
        validator.TestValidate(new RecordTransactionCommand(Actor, Guid.NewGuid(), "", "USD", 100, null, null))
            .ShouldHaveValidationErrorFor(c => c.Type);
        validator.TestValidate(new RecordTransactionCommand(Actor, Guid.NewGuid(), "Contribution!" + new string('x', 30), "USD", 100, null, null))
            .ShouldHaveValidationErrorFor(c => c.Type);
        validator.TestValidate(new RecordTransactionCommand(Actor, Guid.NewGuid(), "contribution", "eur", 100, null, null))
            .ShouldHaveValidationErrorFor(c => c.Currency);
        validator.TestValidate(new RecordTransactionCommand(Actor, Guid.NewGuid(), "contribution", "USD", 0, null, null))
            .ShouldHaveValidationErrorFor(c => c.MinorUnits);
        validator.TestValidate(new RecordTransactionCommand(Actor, Guid.NewGuid(), "contribution", "USD", -5, null, null))
            .ShouldHaveValidationErrorFor(c => c.MinorUnits);
        validator.TestValidate(new RecordTransactionCommand(Actor, Guid.NewGuid(), "contribution", "USD", 100, new string('d', 501), null))
            .ShouldHaveValidationErrorFor(c => c.Description);
        validator.TestValidate(new RecordTransactionCommand(Actor, Guid.NewGuid(), "contribution", "USD", 100, null, Guid.Empty))
            .ShouldHaveValidationErrorFor(c => c.TransferDestinationFundId);

        validator.TestValidate(new RecordTransactionCommand(Actor, Guid.NewGuid(), "transfer", "USD", 100, "note", Guid.NewGuid()))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Submit_Approve_and_Reject_require_an_actor_and_an_existing_transaction()
    {
        var id = Guid.NewGuid();

        new SubmitTransactionCommandValidator().TestValidate(new SubmitTransactionCommand(Guid.Empty, id))
            .ShouldHaveValidationErrorFor(c => c.ActorId);
        new SubmitTransactionCommandValidator().TestValidate(new SubmitTransactionCommand(Actor, Guid.Empty))
            .ShouldHaveValidationErrorFor(c => c.TransactionId);

        new ApproveTransactionCommandValidator().TestValidate(new ApproveTransactionCommand(Guid.Empty, id))
            .ShouldHaveValidationErrorFor(c => c.ActorId);
        new ApproveTransactionCommandValidator().TestValidate(new ApproveTransactionCommand(Actor, Guid.Empty))
            .ShouldHaveValidationErrorFor(c => c.TransactionId);

        new RejectTransactionCommandValidator().TestValidate(
                new RejectTransactionCommand(Guid.Empty, id, "reason"))
            .ShouldHaveValidationErrorFor(c => c.ActorId);
        new RejectTransactionCommandValidator().TestValidate(
                new RejectTransactionCommand(Actor, Guid.Empty, "reason"))
            .ShouldHaveValidationErrorFor(c => c.TransactionId);
        new RejectTransactionCommandValidator().TestValidate(
                new RejectTransactionCommand(Actor, id, new string('r', 501)))
            .ShouldHaveValidationErrorFor(c => c.Reason);

        new SubmitTransactionCommandValidator().TestValidate(new SubmitTransactionCommand(Actor, id))
            .ShouldNotHaveAnyValidationErrors();
    }
}