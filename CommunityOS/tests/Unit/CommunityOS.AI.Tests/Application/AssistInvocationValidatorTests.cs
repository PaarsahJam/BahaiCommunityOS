using CommunityOS.AI.Application.Commands;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace CommunityOS.AI.Tests.Application;

public class AssistInvocationValidatorTests
{
    private readonly AssistInvocationValidator _validator;

    public AssistInvocationValidatorTests()
    {
        _validator = new AssistInvocationValidator();
    }

    [Fact]
    public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        var command = new AssistInvocationCommand
        {
            SubjectId = Guid.NewGuid(),
            Capability = "test",
            Input = "test input"
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WithEmptySubjectId_ShouldHaveError()
    {
        var command = new AssistInvocationCommand
        {
            SubjectId = Guid.Empty,
            Capability = "test",
            Input = "test input"
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.SubjectId);
    }

    [Fact]
    public async Task Validate_WithEmptyCapability_ShouldHaveError()
    {
        var command = new AssistInvocationCommand
        {
            SubjectId = Guid.NewGuid(),
            Capability = "",
            Input = "test input"
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Capability);
    }

    [Fact]
    public async Task Validate_WithEmptyInput_ShouldHaveError()
    {
        var command = new AssistInvocationCommand
        {
            SubjectId = Guid.NewGuid(),
            Capability = "test",
            Input = ""
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Input);
    }

    [Fact]
    public async Task Validate_WithTooLongCapability_ShouldHaveError()
    {
        var command = new AssistInvocationCommand
        {
            SubjectId = Guid.NewGuid(),
            Capability = new string('a', 101),
            Input = "test input"
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Capability);
    }

    [Fact]
    public async Task Validate_WithTooLongInput_ShouldHaveError()
    {
        var command = new AssistInvocationCommand
        {
            SubjectId = Guid.NewGuid(),
            Capability = "test",
            Input = new string('a', 100_001)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Input);
    }
}
