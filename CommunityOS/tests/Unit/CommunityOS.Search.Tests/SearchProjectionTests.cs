using CommunityOS.Search.Domain;
using FluentAssertions;
using Xunit;

namespace CommunityOS.Search.Tests;
public sealed class SearchProjectionTests
{
    [Fact]
    public void RecordProjectionUsesCategoryOnlyAndRejectsSensitiveTextFields()
    {
        var doc = SearchDocument.Create(SearchSourceTypes.Record, Guid.NewGuid(), "birth-registration", "birth-registration", "Active", false, Guid.NewGuid(), DateTime.UtcNow);
        doc.DisplayTitle.Should().Be("birth-registration");
        doc.DisplayTitle.Should().NotContain("name");
    }

    [Fact]
    public void SparseEventMergePreservesExistingFields()
    {
        var unit = Guid.NewGuid(); var created = DateTime.UtcNow;
        var doc = SearchDocument.Create(SearchSourceTypes.Document, Guid.NewGuid(), "Safe title", "policy", "Active", false, unit, created);
        doc.Apply(null, null, "Archived", null, null, false, created.AddMinutes(1));
        doc.DisplayTitle.Should().Be("Safe title"); doc.TypeCode.Should().Be("policy"); doc.OrganizationUnitId.Should().Be(unit); doc.Status.Should().Be("Archived");
    }

    [Fact]
    public void StaleEventIsIgnoredAndEqualEventIsIdempotent()
    {
        var now = DateTime.UtcNow; var doc = SearchDocument.Create(SearchSourceTypes.Record, Guid.NewGuid(), "category", "category", "Active", false, null, now);
        doc.Apply(null, null, "Old", null, null, false, now.AddSeconds(-1)).Should().BeFalse();
        doc.Apply(null, null, "Active", null, null, false, now).Should().BeTrue();
        doc.Status.Should().Be("Active"); doc.IndexedOn.Should().Be(now);
    }

    [Fact]
    public void KnowledgeProjectionIsLiteralAndContainsNoQuestionText()
    {
        var doc = SearchDocument.Create(SearchSourceTypes.KnowledgeQuestion, Guid.NewGuid(), "Question", "question", "Submitted", false, null, DateTime.UtcNow);
        doc.DisplayTitle.Should().Be("Question"); doc.TypeCode.Should().Be("question");
    }
}
