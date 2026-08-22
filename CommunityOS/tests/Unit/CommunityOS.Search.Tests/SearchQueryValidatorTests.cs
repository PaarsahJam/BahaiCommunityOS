using CommunityOS.Search.Application;
using CommunityOS.Search.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace CommunityOS.Search.Tests;
public sealed class SearchQueryValidatorTests
{
    [Fact]
    public void BlankQueryIsInvalid()
    {
        var validator = new SearchQueryValidator();
        var result = validator.TestValidate(new SearchQuery(Guid.NewGuid(), "   ", null, null, null, null, false, 25, 0));
        result.ShouldHaveAnyValidationError().WithErrorCode("SearchQueryRequired");
    }

    [Fact]
    public void UnknownSourceTypeIsInvalid()
    {
        var validator = new SearchQueryValidator();
        var result = validator.TestValidate(new SearchQuery(Guid.NewGuid(), "term", null, "not-a-type", null, null, false, 25, 0));
        result.ShouldHaveValidationErrorFor(x => x.SourceType);
    }

    [Fact]
    public void UnknownTypeInTypesListIsInvalid()
    {
        var validator = new SearchQueryValidator();
        var result = validator.TestValidate(new SearchQuery(Guid.NewGuid(), "term", ["record", "bogus"], null, null, null, false, 25, 0));
        result.ShouldHaveValidationErrorFor(x => x.Types);
    }

    [Fact]
    public void LimitBelowOneIsInvalid()
    {
        var validator = new SearchQueryValidator();
        var result = validator.TestValidate(new SearchQuery(Guid.NewGuid(), "term", null, null, null, null, false, 0, 0));
        result.ShouldHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void NegativeOffsetIsInvalid()
    {
        var validator = new SearchQueryValidator();
        var result = validator.TestValidate(new SearchQuery(Guid.NewGuid(), "term", null, null, null, null, false, 25, -1));
        result.ShouldHaveValidationErrorFor(x => x.Offset);
    }

    [Fact]
    public void ValidRequestPasses()
    {
        var validator = new SearchQueryValidator();
        var result = validator.TestValidate(new SearchQuery(Guid.NewGuid(), "term", ["record"], "record",
            Guid.NewGuid(), ["Active", "Published"], true, 50, 10));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UnknownReindexSourceTypeIsInvalid()
    {
        var validator = new ReindexCommandValidator();
        var result = validator.TestValidate(new ReindexCommand(Guid.NewGuid(), "nope"));
        result.ShouldHaveValidationErrorFor(x => x.SourceType);
    }
}
