using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using CommunityOS.Knowledge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CommunityOS.Knowledge.IntegrationTests.Persistence;

/// <summary>
/// Verifies the EF Core mapping and the InitialCreateKnowledge migration
/// against a real PostgreSQL instance (Testcontainers). Configuration errors
/// (snake_case, owned child tables, enums stored as ints) surface here rather
/// than in production.
/// </summary>
public sealed class KnowledgePersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("communityos_knowledge")
        .WithUsername("communityos")
        .WithPassword("communityos")
        .Build();

    private string? _connectionString;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private KnowledgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(_connectionString!, npgsql => npgsql
                .MigrationsAssembly(typeof(KnowledgeDbContext).Assembly.FullName))
            .Options;
        return new KnowledgeDbContext(options);
    }

    private async Task<List<string>> QueryStringsAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var results = new List<string>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));

        return results;
    }

    [Fact]
    public async Task Migrations_create_all_knowledge_tables_in_knowledge_schema()
    {
        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'knowledge' ORDER BY tablename;");

        tables.Should().Contain(
        [
            "ai_suggestions",
            "answers",
            "categories",
            "discussions",
            "editions",
            "organization_unit_references",
            "passages",
            "questions",
            "references",
            "topics",
            "works",
            "answer_revisions",
            "comments",
            "moderation_flags",
            "passage_revisions",
            "question_lifecycle_events",
            "tags"
        ]);
    }

    [Fact]
    public async Task Migrations_can_be_applied_idempotently()
    {
        await using var db = CreateContext();

        await db.Database.MigrateAsync();

        var tables = await QueryStringsAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'knowledge';");
        tables.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Work_and_Edition_repository_roundtrip_verification_state()
    {
        await using var db = CreateContext();
        var workRepo = new WorkRepository(db);
        var editionRepo = new EditionRepository(db);

        var work = Work.Create("Kitab-i-Iqan", "book", "fa", "fa");
        await workRepo.AddAsync(work);

        var edition = Edition.Import(work.Id, "en", "Shoghi Effendi", null, 1931);
        await editionRepo.AddAsync(edition);

        var fetched = await editionRepo.GetByIdAsync(edition.Id);
        fetched.Should().NotBeNull();
        fetched!.WorkId.Should().Be(work.Id);
        fetched.Language.Should().Be("en");
        fetched.Translator.Should().Be("Shoghi Effendi");
        fetched.Verified.Should().BeFalse();

        fetched.Verify();
        await editionRepo.UpdateAsync(fetched);

        var verified = await editionRepo.GetByIdAsync(edition.Id);
        verified!.Verified.Should().BeTrue();
    }

    [Fact]
    public async Task Passage_repository_preserves_revision_history()
    {
        await using var db = CreateContext();
        var editionRepo = new EditionRepository(db);
        var passageRepo = new PassageRepository(db);

        var edition = Edition.Import(Guid.NewGuid(), "en", null, null, 1999, verified: true);
        await editionRepo.AddAsync(edition);

        var passage = Passage.Import(edition.Id, "2:1", "Blessed is the man", 0);
        await passageRepo.AddAsync(passage);

        var fetched = await passageRepo.GetByIdAsync(passage.Id);
        fetched.Should().NotBeNull();
        fetched!.Revision.Should().Be(1);
        fetched.Revisions.Should().ContainSingle(r => r.Revision == 1);

        fetched.Correct("Corrected text");
        await passageRepo.UpdateAsync(fetched);

        var corrected = await passageRepo.GetByIdAsync(passage.Id);
        corrected!.Revision.Should().Be(2);
        corrected.Text.Should().Be("Corrected text");
        corrected.Revisions.Should().HaveCount(2);
        corrected.Revisions[0].Text.Should().Be("Blessed is the man");

        var listed = await passageRepo.ListByEditionAsync(edition.Id);
        listed.Should().ContainSingle(p => p.Id == passage.Id);
    }

    [Fact]
    public async Task Question_repository_preserves_lifecycle_tags_flags_and_accepted_answer()
    {
        await using var db = CreateContext();
        var questionRepo = new QuestionRepository(db);
        var answerRepo = new AnswerRepository(db);

        var question = Question.Create(
            "How do I study?",
            "What is a recommended approach?",
            Guid.NewGuid(),
            categoryId: null,
            organizationUnitId: Guid.NewGuid(),
            tags: ["study", "kitab"]);
        await questionRepo.AddAsync(question);

        var answer = Answer.Create(question.Id, Guid.NewGuid(), "An answer");
        await answerRepo.AddAsync(answer);

        var fetched = await questionRepo.GetByIdAsync(question.Id);
        fetched.Should().NotBeNull();
        fetched!.Tags.Should().HaveCount(2);
        fetched.LifecycleEvents.Should().ContainSingle();

        fetched.Submit();
        fetched.Publish();
        fetched.Flag("Needs a citation");
        fetched.AcceptAnswer(answer.Id);
        await questionRepo.UpdateAsync(fetched);

        answer.MarkAccepted();
        await answerRepo.UpdateAsync(answer);

        var updated = await questionRepo.GetByIdAsync(question.Id);
        updated!.Status.Should().Be(QuestionStatus.Published);
        updated.AcceptedAnswerId.Should().Be(answer.Id);
        updated.ModerationFlags.Should().ContainSingle(f => f.Reason == "Needs a citation");
        updated.LifecycleEvents.Should().HaveCount(3);
    }

    [Fact]
    public async Task Answer_repository_preserves_revision_history_and_source()
    {
        await using var db = CreateContext();
        var answerRepo = new AnswerRepository(db);

        var answer = Answer.Create(Guid.NewGuid(), Guid.NewGuid(), "First draft");
        answer.Update("Revised");
        await answerRepo.AddAsync(answer);

        var fetched = await answerRepo.GetByIdAsync(answer.Id);
        fetched.Should().NotBeNull();
        fetched!.Revision.Should().Be(2);
        fetched.Source.Should().Be(AnswerSource.Member);
        fetched.Revisions.Should().HaveCount(2);
        fetched.Revisions[1].Body.Should().Be("Revised");
    }

    [Fact]
    public async Task AiSuggestion_repository_preserves_review_state()
    {
        await using var db = CreateContext();
        var suggestionRepo = new AiSuggestionRepository(db);

        var suggestion = AiSuggestion.Request(
            Guid.NewGuid(), Guid.NewGuid(), "Draft body", "gpt-4o", "1");
        await suggestionRepo.AddAsync(suggestion);

        var fetched = await suggestionRepo.GetByIdAsync(suggestion.Id);
        fetched.Should().NotBeNull();
        fetched!.ReviewState.Should().Be(AiSuggestionReviewState.Suggested);

        fetched.Review(AiSuggestionReviewState.Accepted);
        await suggestionRepo.UpdateAsync(fetched);

        var reviewed = await suggestionRepo.GetByIdAsync(suggestion.Id);
        reviewed!.ReviewState.Should().Be(AiSuggestionReviewState.Accepted);
        reviewed.ReviewedOn.Should().NotBeNull();
    }

    [Fact]
    public async Task Discussion_repository_preserves_comments_and_moderation_state()
    {
        await using var db = CreateContext();
        var discussionRepo = new DiscussionRepository(db);

        var discussion = Discussion.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Thread title", Guid.NewGuid());
        discussion.AddComment(Guid.NewGuid(), "A comment");
        await discussionRepo.AddAsync(discussion);

        var fetched = await discussionRepo.GetByIdAsync(discussion.Id);
        fetched.Should().NotBeNull();
        fetched!.Comments.Should().ContainSingle(c => c.Body == "A comment");

        fetched.Hide();
        await discussionRepo.UpdateAsync(fetched);

        var hidden = await discussionRepo.GetByIdAsync(discussion.Id);
        hidden!.Status.Should().Be(DiscussionStatus.Hidden);
    }
}