using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Knowledge.Application;
using CommunityOS.Knowledge.Application.Commands;
using CommunityOS.Knowledge.Application.Permissions;
using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CommunityOS.Knowledge.Tests.Security;

/// <summary>
/// Security regression tests. These lock in the authorization boundaries of
/// the Knowledge bounded context: every command flows through the
/// Authorization guard, the guard is fail-closed (a denied or unreachable
/// evaluator blocks the operation and nothing is persisted), and library
/// operations use global contexts while question/answer/AI/discussion
/// operations are scoped to the question's organization unit.
/// </summary>
public class KnowledgeSecurityRegressionTests
{
    private static readonly Guid ActorId = Guid.NewGuid();

    private sealed class Harness
    {
        public RepoSet Repos { get; } = RepoSet.Create();
        public ServiceProvider Provider { get; }

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddKnowledgeApplication();

            services.AddScoped(_ => Repos.Works);
            services.AddScoped(_ => Repos.Editions);
            services.AddScoped(_ => Repos.Passages);
            services.AddScoped(_ => Repos.Questions);
            services.AddScoped(_ => Repos.Answers);
            services.AddScoped(_ => Repos.References);
            services.AddScoped(_ => Repos.Discussions);
            services.AddScoped(_ => Repos.Suggestions);
            services.AddScoped(_ => Repos.Categories);
            services.AddScoped(_ => Repos.Topics);
            services.AddScoped(_ => Repos.OrganizationUnitReferences);
            services.AddScoped(_ => Repos.Evaluator);
            services.AddScoped<AuthorizationGuard>();

            Provider = services.BuildServiceProvider();
        }

        public ISender Sender => Provider.GetRequiredService<ISender>();

        /// <summary>Denies every authorization request (fail-closed default).</summary>
        public void DenyAll() => Repos.Evaluator.EvaluateAsync(
                Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow));
    }

    private sealed record RepoSet(
        IWorkRepository Works,
        IEditionRepository Editions,
        IPassageRepository Passages,
        IQuestionRepository Questions,
        IAnswerRepository Answers,
        IReferenceRepository References,
        IDiscussionRepository Discussions,
        IAiSuggestionRepository Suggestions,
        ICategoryRepository Categories,
        ITopicRepository Topics,
        IOrganizationUnitReferenceRepository OrganizationUnitReferences,
        IAuthorizationEvaluator Evaluator)
    {
        public static RepoSet Create() => new(
            Substitute.For<IWorkRepository>(),
            Substitute.For<IEditionRepository>(),
            Substitute.For<IPassageRepository>(),
            Substitute.For<IQuestionRepository>(),
            Substitute.For<IAnswerRepository>(),
            Substitute.For<IReferenceRepository>(),
            Substitute.For<IDiscussionRepository>(),
            Substitute.For<IAiSuggestionRepository>(),
            Substitute.For<ICategoryRepository>(),
            Substitute.For<ITopicRepository>(),
            Substitute.For<IOrganizationUnitReferenceRepository>(),
            Substitute.For<IAuthorizationEvaluator>());
    }

    private static Harness CreateHarness() => new();

    private static Question StubQuestion(Guid? unitId) => Question.Create(
        "How do I study?",
        "What is a recommended approach?",
        ActorId,
        categoryId: null,
        organizationUnitId: unitId);

    // --- Fail-closed behavior ---

    [Fact]
    public async Task CreateWork_is_denied_when_evaluator_denies()
    {
        var h = CreateHarness();
        h.DenyAll();

        var act = () => h.Sender.Send(new CreateWorkCommand(
            ActorId, "Kitab-i-Iqan", "book", "fa", "fa"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Works.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task CreateWork_is_denied_when_evaluator_throws()
    {
        var h = CreateHarness();
        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AuthorizationDecision>(new HttpRequestException("authz down")));

        var act = () => h.Sender.Send(new CreateWorkCommand(
            ActorId, "Kitab-i-Iqan", "book", "fa", "fa"));

        await act.Should().ThrowAsync<Exception>();
        await h.Repos.Works.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    // --- Library operations use a global context ---

    [Fact]
    public async Task CreateWork_requires_library_import_and_uses_global_context()
    {
        var h = CreateHarness();
        h.DenyAll();

        var act = () => h.Sender.Send(new CreateWorkCommand(
            ActorId, "Kitab-i-Iqan", "book", "fa", "fa"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();

        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == KnowledgePermissions.LibraryImport &&
                r.Context.OrganizationUnitId == null &&
                r.Context.ResourceType == "work"), Arg.Any<CancellationToken>());
    }

    // --- Question operations are org-scoped ---

    [Fact]
    public async Task SubmitQuestion_throws_without_update_grant_on_owning_unit()
    {
        var h = CreateHarness();
        h.DenyAll();
        var unit = Guid.NewGuid();
        var question = StubQuestion(unit);
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);

        var act = () => h.Sender.Send(new SubmitQuestionCommand(ActorId, question.Id));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Questions.DidNotReceive().UpdateAsync(Arg.Any<Question>(), Arg.Any<CancellationToken>());

        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == KnowledgePermissions.QuestionUpdate &&
                r.Context.OrganizationUnitId == unit &&
                r.Context.ResourceId == question.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishQuestion_requires_moderation_review()
    {
        var h = CreateHarness();
        h.DenyAll();
        var question = StubQuestion(null);
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);

        var act = () => h.Sender.Send(new PublishQuestionCommand(ActorId, question.Id));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    [Fact]
    public async Task AcceptAnswer_requires_moderation_review_on_question_unit()
    {
        var h = CreateHarness();
        h.DenyAll();
        var unit = Guid.NewGuid();
        var question = StubQuestion(unit);
        var answer = Answer.Create(question.Id, ActorId, "An answer");
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);
        h.Repos.Answers.GetByIdAsync(answer.Id, Arg.Any<CancellationToken>()).Returns(answer);

        var act = () => h.Sender.Send(new AcceptAnswerCommand(ActorId, question.Id, answer.Id));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();

        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == KnowledgePermissions.ModerationReview &&
                r.Context.OrganizationUnitId == unit &&
                r.Context.ResourceId == question.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAnswer_requires_answer_create_on_question_unit()
    {
        var h = CreateHarness();
        h.DenyAll();
        var unit = Guid.NewGuid();
        var question = StubQuestion(unit);
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);

        var act = () => h.Sender.Send(new CreateAnswerCommand(
            ActorId, question.Id, "Answer body", null));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();

        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == KnowledgePermissions.AnswerCreate &&
                r.Context.OrganizationUnitId == unit), Arg.Any<CancellationToken>());
    }

    // --- AI suggestions are guarded and human-reviewed ---

    [Fact]
    public async Task RequestAiSuggestion_requires_ai_suggest_on_question_unit()
    {
        var h = CreateHarness();
        h.DenyAll();
        var unit = Guid.NewGuid();
        var question = StubQuestion(unit);
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);

        var act = () => h.Sender.Send(new RequestAiSuggestionCommand(
            ActorId, question.Id, "gpt-4o", "Draft an answer", null));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();

        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == KnowledgePermissions.AiSuggest &&
                r.Context.OrganizationUnitId == unit), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptAiSuggestion_requires_ai_review_and_denies_without_grant()
    {
        var h = CreateHarness();
        h.DenyAll();
        var unit = Guid.NewGuid();
        var question = StubQuestion(unit);
        var suggestion = AiSuggestion.Request(question.Id, unit, "Draft", "gpt-4o", "1");
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);
        h.Repos.Suggestions.GetByIdAsync(suggestion.Id, Arg.Any<CancellationToken>()).Returns(suggestion);

        var act = () => h.Sender.Send(new AcceptAiSuggestionCommand(ActorId, suggestion.Id));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Suggestions.DidNotReceive().UpdateAsync(Arg.Any<AiSuggestion>(), Arg.Any<CancellationToken>());
        await h.Repos.Answers.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    // --- Discussions are guarded ---

    [Fact]
    public async Task ModerateDiscussion_requires_moderate_grant()
    {
        var h = CreateHarness();
        h.DenyAll();
        var question = StubQuestion(null);
        var discussion = Discussion.Create(question.Id, null, "Thread", ActorId);
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);
        h.Repos.Discussions.GetByIdAsync(discussion.Id, Arg.Any<CancellationToken>()).Returns(discussion);

        var act = () => h.Sender.Send(new ModerateDiscussionCommand(ActorId, discussion.Id, "hide"));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Discussions.DidNotReceive().UpdateAsync(Arg.Any<Discussion>(), Arg.Any<CancellationToken>());
    }

    // --- Missing aggregates surface domain exceptions, not permission leaks ---

    [Fact]
    public async Task SubmitQuestion_throws_QuestionNotFound_when_missing()
    {
        var h = CreateHarness();
        h.DenyAll();

        var act = () => h.Sender.Send(new SubmitQuestionCommand(ActorId, Guid.NewGuid()));

        await act.Should().ThrowAsync<CommunityOS.Knowledge.Domain.Exceptions.QuestionNotFoundException>();
        await h.Repos.Evaluator.DidNotReceiveWithAnyArgs().EvaluateAsync(default!, default);
    }
}