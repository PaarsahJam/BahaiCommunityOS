using CommunityOS.Contracts.Documents;
using CommunityOS.Contracts.Knowledge;
using CommunityOS.Contracts.Records;
using CommunityOS.Contracts.Workflow;
using CommunityOS.Search.Infrastructure;
using CommunityOS.Search.Infrastructure.Integration;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CommunityOS.Search.Tests;
public sealed class SearchConsumerTests
{
    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task RecordsCreatedMapsOnlyContractSafeMetadata()
    {
        var writer = Substitute.For<ISearchProjectionWriter>();
        var consumer = new RecordsIndexConsumer(writer, NullLogger<RecordsIndexConsumer>.Instance);
        var id = Guid.NewGuid(); var unit = Guid.NewGuid();
        await consumer.Consume(Context(new RecordCreated(id, "membership", "Active", "person", Guid.NewGuid(), unit, Guid.NewGuid(), DateTime.UtcNow)));
        // Subject type/id and creator never reach the projection: category code only.
        await writer.Received(1).UpsertAsync("record", id, "membership", "membership", "Active", null, unit, true, Arg.Any<DateTime>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordClassificationDoesNotOverwriteSparseFields()
    {
        var writer = Substitute.For<ISearchProjectionWriter>();
        var consumer = new RecordsIndexConsumer(writer, NullLogger<RecordsIndexConsumer>.Instance);
        var id = Guid.NewGuid();
        await consumer.Consume(Context(new RecordClassified(id, "restricted", true, DateTime.UtcNow)));
        await writer.Received(1).UpsertAsync("record", id, null, null, null, true, null, false, Arg.Any<DateTime>(), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DocumentCreatedCarriesTitleAndOwnerType()
    {
        var writer = Substitute.For<ISearchProjectionWriter>();
        var consumer = new DocumentsIndexConsumer(writer, NullLogger<DocumentsIndexConsumer>.Instance);
        var id = Guid.NewGuid(); var unit = Guid.NewGuid();
        await consumer.Consume(Context(new DocumentCreated(id, "Assembly Minutes", "Active", unit, "committee", Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)));
        await writer.Received(1).UpsertAsync("document", id, "Assembly Minutes", "committee", "Active", null, unit, true, Arg.Any<DateTime>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WorkflowTaskCreatedUsesDefinitionCodeAsTitle()
    {
        var writer = Substitute.For<ISearchProjectionWriter>();
        var consumer = new WorkflowIndexConsumer(writer, NullLogger<WorkflowIndexConsumer>.Instance);
        var id = Guid.NewGuid(); var unit = Guid.NewGuid();
        await consumer.Consume(Context(new WorkflowTaskCreated(id, "membership-review", "person", Guid.NewGuid(), unit, Guid.NewGuid(), DateTime.UtcNow)));
        await writer.Received(1).UpsertAsync("workflow-task", id, "membership-review", "membership-review", "Created", false, unit, true, Arg.Any<DateTime>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task KnowledgeQuestionIsIndexedWithLiteralTitleOnly()
    {
        var writer = Substitute.For<ISearchProjectionWriter>();
        var consumer = new KnowledgeIndexConsumer(writer, NullLogger<KnowledgeIndexConsumer>.Instance);
        var id = Guid.NewGuid(); var unit = Guid.NewGuid();
        await consumer.Consume(Context(new QuestionSubmitted(id, Guid.NewGuid(), unit, DateTime.UtcNow)));
        await writer.Received(1).UpsertAsync("knowledge-question", id, "Question", "question", "Submitted", false, unit, true, Arg.Any<DateTime>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuestionFlaggedIgnoresFlagReasonText()
    {
        var writer = Substitute.For<ISearchProjectionWriter>();
        var consumer = new KnowledgeIndexConsumer(writer, NullLogger<KnowledgeIndexConsumer>.Instance);
        var id = Guid.NewGuid();
        await consumer.Consume(Context(new QuestionFlagged(id, "some private reason", DateTime.UtcNow)));
        await writer.Received(1).UpsertAsync("knowledge-question", id, "Question", "question", "Flagged", false, null, false, Arg.Any<DateTime>(), false, Arg.Any<CancellationToken>());
    }
}
