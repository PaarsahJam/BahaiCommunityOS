using CommunityOS.Search.API.Extensions;
using Xunit;

namespace CommunityOS.Search.Tests;

public class SearchQueryFilterParserTests
{
    [Fact]
    public void NullInputReturnsNull() => Assert.Null(SearchQueryFilterParser.Normalize(null));

    [Fact]
    public void EmptyArrayReturnsNull() => Assert.Null(SearchQueryFilterParser.Normalize([]));

    [Fact]
    public void SingleValueIsPreserved() => Assert.Equal(["record"], SearchQueryFilterParser.Normalize(["record"])!);

    [Fact]
    public void CommaSeparatedSingleParameterProducesAllValues()
        => Assert.Equal(["record", "document"], SearchQueryFilterParser.Normalize(["record,document"])!);

    [Fact]
    public void RepeatedParametersAggregateIntoOneCollection()
        => Assert.Equal(["record", "document"], SearchQueryFilterParser.Normalize(["record", "document"])!);

    [Fact]
    public void RepeatedStatusParametersAggregateIntoOneCollection()
        => Assert.Equal(["active", "archived"], SearchQueryFilterParser.Normalize(["active", "archived"])!);

    [Fact]
    public void MixedRepeatedAndCommaSeparatedEntriesFlattenTogether()
        => Assert.Equal(["record", "document", "workflow-task"], SearchQueryFilterParser.Normalize(["record,document", "workflow-task"])!);

    [Fact]
    public void EntriesAreTrimmedAndEmptySegmentsAreRemoved()
        => Assert.Equal(["record", "document"], SearchQueryFilterParser.Normalize([" record , ", "", "  ", "document"])!);

    [Fact]
    public void WhitespaceOnlyInputReturnsNull() => Assert.Null(SearchQueryFilterParser.Normalize(["", "   "]));
}
