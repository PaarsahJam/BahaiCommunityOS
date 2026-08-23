using CommunityOS.Correspondence.Application;
using CommunityOS.Correspondence.Application.Validators;
using CommunityOS.Correspondence.Domain;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace CommunityOS.Correspondence.Tests.Application;

public sealed class CorrespondenceValidatorTests
{
    private static CreateLetterCommand CreateCommand(
        string? kind = "person", Guid? personId = null, string? displayLine = null) =>
        new(Guid.NewGuid(), "general", "normal", "S", "B", Guid.NewGuid(),
            [new RecipientInput(kind!, personId ?? Guid.NewGuid(), null, displayLine)],
            null, null);

    [Fact]
    public void Create_requires_category_subject_body_and_recipients_without_a_template()
    {
        var validator = new CreateLetterValidator();
        var result = validator.TestValidate(CreateCommand());
        result.ShouldNotHaveAnyValidationErrors();

        var noRecipients = CreateCommand() with { Recipients = [] };
        validator.TestValidate(noRecipients).ShouldHaveValidationErrorFor(c => c.Recipients);
    }

    [Fact]
    public void Create_enforces_recipient_kind_exclusivity()
    {
        var validator = new CreateLetterValidator();

        validator.TestValidate(CreateCommand(kind: "person")).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(CreateCommand(kind: "person", personId: Guid.Empty))
            .ShouldHaveValidationErrorFor("Recipients[0].PersonId");
        validator.TestValidate(CreateCommand(kind: "unit"))
            .ShouldHaveAnyValidationError();
        validator.TestValidate(CreateCommand(kind: "external", personId: null, displayLine: "line"))
            .ShouldHaveValidationErrorFor("Recipients[0].PersonId");
        validator.TestValidate(CreateCommand(kind: "carrier-pigeon", personId: null))
            .ShouldHaveValidationErrorFor("Recipients[0].Kind");
    }

    [Fact]
    public void Dispatch_is_manual_only_at_this_gate()
    {
        var validator = new DispatchLetterValidator();
        var actor = Guid.NewGuid();

        validator.TestValidate(new DispatchLetterCommand(actor, Guid.NewGuid(), "manual"))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new DispatchLetterCommand(actor, Guid.NewGuid(), "email"))
            .ShouldHaveValidationErrorFor(c => c.MethodCode);
    }

    [Fact]
    public void Delivery_outcomes_validate_the_reason_vocabulary_per_outcome()
    {
        var validator = new RecordDeliveryOutcomeValidator();
        var actor = Guid.NewGuid();

        validator.TestValidate(new RecordDeliveryOutcomeCommand(actor, Guid.NewGuid(), "confirmed", null))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new RecordDeliveryOutcomeCommand(actor, Guid.NewGuid(), "failed", "bad-address"))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new RecordDeliveryOutcomeCommand(actor, Guid.NewGuid(), "failed", "made-up"))
            .ShouldHaveValidationErrorFor(c => c.ReasonCode);
        validator.TestValidate(new RecordDeliveryOutcomeCommand(actor, Guid.NewGuid(), "confirmed", "bad-address"))
            .ShouldHaveValidationErrorFor(c => c.ReasonCode);
        validator.TestValidate(new RecordDeliveryOutcomeCommand(actor, Guid.NewGuid(), "lost", null))
            .ShouldHaveValidationErrorFor(c => c.Outcome);
    }

    [Fact]
    public void Holds_validate_type_and_reason_vocabularies()
    {
        var validator = new PlaceHoldsValidator();
        var actor = Guid.NewGuid();

        validator.TestValidate(new PlaceHoldsCommand(actor, [Guid.NewGuid()], LetterHold.HoldTypeLegal, "investigation"))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new PlaceHoldsCommand(actor, [Guid.NewGuid()], "temporary", "investigation"))
            .ShouldHaveValidationErrorFor(c => c.HoldType);
        validator.TestValidate(new PlaceHoldsCommand(actor, [Guid.NewGuid()], LetterHold.HoldTypeLegal, "because"))
            .ShouldHaveValidationErrorFor(c => c.ReasonCode);
    }

    [Fact]
    public void Purge_rejects_negative_batch_sizes_but_accepts_the_default_marker()
    {
        var validator = new PurgeExpiredValidator();
        var actor = Guid.NewGuid();

        validator.TestValidate(new PurgeExpiredLettersCommand(actor, 0)).IsValid.Should().BeTrue();
        validator.TestValidate(new PurgeExpiredLettersCommand(actor, 5000)).IsValid.Should().BeTrue();
        validator.TestValidate(new PurgeExpiredLettersCommand(actor, -1))
            .ShouldHaveValidationErrorFor(c => c.MaxBatchSize);
    }

    [Fact]
    public void Export_validates_format_and_positive_max_rows()
    {
        var validator = new ExportLettersValidator();
        var actor = Guid.NewGuid();
        var filters = new LetterQueryFilters(null, null, null, null, null, null);

        validator.TestValidate(new ExportLettersCommand(actor, "csv", filters, false, null))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ExportLettersCommand(actor, "pdf", filters, false, null))
            .ShouldHaveValidationErrorFor(c => c.Format);
        validator.TestValidate(new ExportLettersCommand(actor, "csv", filters, false, 0))
            .ShouldHaveValidationErrorFor(c => c.MaxRows);
    }

    [Fact]
    public void Cancellation_reasons_come_from_the_fixed_vocabulary()
    {
        var validator = new CancelLetterValidator();

        validator.TestValidate(new CancelLetterCommand(Guid.NewGuid(), Guid.NewGuid(), "withdrawn-by-institution"))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new CancelLetterCommand(Guid.NewGuid(), Guid.NewGuid(), "misc"))
            .ShouldHaveValidationErrorFor(c => c.ReasonCode);
    }
}
