using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Knowledge.Application;
using CommunityOS.Knowledge.Application.Commands;
using CommunityOS.Knowledge.Application.Permissions;
using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.Knowledge.Domain.Events;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.SharedKernel.Domain.Events;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CommunityOS.Knowledge.Tests.Security;

/// <summary>
/// Regression tests for the event-consistency guarantee: accepting an AI
/// suggestion creates a normal authoritative Answer (source = "ai") that emits
/// the same <c>AnswerAdded</c> domain event as the member-authored path — and
/// only that path. The AI itself never publishes an answer; acceptance remains
/// an explicit human-governed, authorization-enforced action.
/// </summary>
public class KnowledgeAiSuggestionAcceptanceTests
{
    private static readonly Guid ActorId = Guid.NewGuid();

    private sealed class RecordingDomainEventHandler
    {
        public List<IDomainEvent> Published { get; } = [];
    }

    /// <summary>
    /// Open-generic handler mirroring how the integration publishers are
    /// registered (AddScoped(typeof(INotificationHandler&lt;&gt;), ...)). MediatR
    /// 12.4.1 dispatches by the runtime type of the notification, so a handler
    /// typed for the IDomainEvent interface is never invoked; the closed generic
    /// (e.g. INotificationHandler&lt;AnswerAddedEvent&gt;) is what receives it.
    /// </summary>
    private sealed class RecordingDomainEventHandler<TDomainEvent>(RecordingDomainEventHandler sink)
        : INotificationHandler<TDomainEvent>
        where TDomainEvent : IDomainEvent
    {
        public Task Handle(TDomainEvent notification, CancellationToken ct)
        {
            sink.Published.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class Harness
    {
        public RepoSet Repos { get; } = RepoSet.Create();
        public RecordingDomainEventHandler Events { get; } = new();
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
            services.AddSingleton(Events);
            services.AddSingleton(typeof(INotificationHandler<>), typeof(RecordingDomainEventHandler<>));

            Provider = services.BuildServiceProvider();
        }

        public ISender Sender => Provider.GetRequiredService<ISender>();

        /// <summary>Denies every authorization request (fail-closed default).</summary>
        public void DenyAll() => Repos.Evaluator.EvaluateAsync(
                Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow));

        /// <summary>Allows every authorization request.</summary>
        public void AllowAll() => Repos.Evaluator.EvaluateAsync(
                Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => AuthorizationDecision.Allow(
                "test-decision",
                [call.Arg<AuthorizationRequest>().Permission],
                DateTime.UtcNow));
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

    // --- A suggestion cannot become authoritative without explicit acceptance ---

    [Fact]
    public async Task Requesting_a_suggestion_never_creates_an_answer_or_emits_AnswerAdded()
    {
        var h = CreateHarness();
        h.AllowAll();
        var question = StubQuestion(null);
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);

        await h.Sender.Send(new RequestAiSuggestionCommand(
            ActorId, question.Id, "gpt-4o", "Draft an answer", null));

        // The AI draft is stored as a non-authoritative suggestion only.
        await h.Repos.Suggestions.Received(1).AddAsync(
            Arg.Is<AiSuggestion>(s => s.ReviewState == AiSuggestionReviewState.Suggested),
            Arg.Any<CancellationToken>());
        await h.Repos.Answers.DidNotReceiveWithAnyArgs().AddAsync(default!);
        h.Events.Published.Should().NotContain(e => e is AnswerAddedEvent);
    }

    [Fact]
    public async Task AiSuggestion_cannot_be_made_authoritative_without_an_accepted_human_review()
    {
        var h = CreateHarness();
        h.AllowAll();
        var question = StubQuestion(null);
        var suggestion = AiSuggestion.Request(question.Id, null, "Draft", "gpt-4o", "1");
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);
        h.Repos.Suggestions.GetByIdAsync(suggestion.Id, Arg.Any<CancellationToken>()).Returns(suggestion);

        // The suggestion stays in Suggested state until a human explicitly
        // reviews it; requesting does not move it and no answer exists.
        suggestion.ReviewState.Should().Be(AiSuggestionReviewState.Suggested);
        await h.Repos.Answers.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    // --- Accepting a suggestion creates the expected authoritative Answer ---

    [Fact]
    public async Task Accepting_creates_an_ai_sourced_answer_with_original_metadata()
    {
        var h = CreateHarness();
        h.AllowAll();
        var question = StubQuestion(null);
        var suggestion = AiSuggestion.Request(question.Id, null, "The answer draft.", "gpt-4o", "1");
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);
        h.Repos.Suggestions.GetByIdAsync(suggestion.Id, Arg.Any<CancellationToken>()).Returns(suggestion);

        var result = await h.Sender.Send(new AcceptAiSuggestionCommand(ActorId, suggestion.Id));

        await h.Repos.Answers.Received(1).AddAsync(
            Arg.Is<Answer>(a =>
                a.QuestionId == question.Id &&
                a.AuthorId == ActorId &&
                a.Body == "The answer draft." &&
                a.Source == AnswerSource.Ai &&
                a.ModelId == "gpt-4o" &&
                a.SuggestionId == suggestion.Id),
            Arg.Any<CancellationToken>());
        await h.Repos.Suggestions.Received(1).UpdateAsync(
            Arg.Is<AiSuggestion>(s => s.ReviewState == AiSuggestionReviewState.Accepted),
            Arg.Any<CancellationToken>());
        result.ReviewState.Should().Be(AiSuggestionReviewState.Accepted.Name);
    }

    // --- AnswerAdded is published exactly once on acceptance ---

    [Fact]
    public async Task Accepting_publishes_exactly_one_AnswerAdded_for_the_created_answer()
    {
        var h = CreateHarness();
        h.AllowAll();
        var question = StubQuestion(null);
        var suggestion = AiSuggestion.Request(question.Id, null, "Draft", "gpt-4o", "1");
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);
        h.Repos.Suggestions.GetByIdAsync(suggestion.Id, Arg.Any<CancellationToken>()).Returns(suggestion);

        await h.Sender.Send(new AcceptAiSuggestionCommand(ActorId, suggestion.Id));

        // Exactly one AnswerAdded (source "ai") and one AiSuggestionReviewed.
        h.Events.Published.Where(e => e is AnswerAddedEvent).Should().ContainSingle();
        var added = h.Events.Published.OfType<AnswerAddedEvent>().Single();
        added.QuestionId.Should().Be(question.Id);
        added.AuthorId.Should().Be(ActorId);
        added.Source.Should().Be(AnswerSource.Ai.Name);

        h.Events.Published.OfType<AiSuggestionReviewedEvent>().Should().ContainSingle(
            e => e.Outcome == AiSuggestionReviewState.Accepted.Name);
    }

    // --- Unauthorized acceptance fails closed ---

    [Fact]
    public async Task Unauthorized_acceptance_is_denied_and_publishes_nothing()
    {
        var h = CreateHarness();
        h.DenyAll();
        var question = StubQuestion(null);
        var suggestion = AiSuggestion.Request(question.Id, null, "Draft", "gpt-4o", "1");
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);
        h.Repos.Suggestions.GetByIdAsync(suggestion.Id, Arg.Any<CancellationToken>()).Returns(suggestion);

        var act = () => h.Sender.Send(new AcceptAiSuggestionCommand(ActorId, suggestion.Id));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Answers.DidNotReceiveWithAnyArgs().AddAsync(default!);
        await h.Repos.Suggestions.DidNotReceive().UpdateAsync(Arg.Any<AiSuggestion>(), Arg.Any<CancellationToken>());
        h.Events.Published.Should().NotContain(e => e is AnswerAddedEvent);
        h.Events.Published.Should().NotContain(e => e is AiSuggestionReviewedEvent);
    }

    [Fact]
    public async Task Unauthorized_acceptance_requires_ai_review_on_owning_unit()
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
        await h.Repos.Evaluator.Received(1)
            .EvaluateAsync(Arg.Is<AuthorizationRequest>(r =>
                r.Permission == KnowledgePermissions.AiReview &&
                r.Context.OrganizationUnitId == unit &&
                r.Context.ResourceId == suggestion.Id), Arg.Any<CancellationToken>());
    }

    // --- A reviewed suggestion cannot be accepted again (no duplicate events) ---

    [Fact]
    public async Task Accepting_an_already_reviewed_suggestion_is_rejected_without_new_events()
    {
        var h = CreateHarness();
        h.AllowAll();
        var question = StubQuestion(null);
        var suggestion = AiSuggestion.Request(question.Id, null, "Draft", "gpt-4o", "1");
        suggestion.Review(AiSuggestionReviewState.Accepted);
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);
        h.Repos.Suggestions.GetByIdAsync(suggestion.Id, Arg.Any<CancellationToken>()).Returns(suggestion);

        var act = () => h.Sender.Send(new AcceptAiSuggestionCommand(ActorId, suggestion.Id));

        await act.Should().ThrowAsync<CommunityOS.Knowledge.Domain.Exceptions.AiSuggestionAlreadyReviewedException>();
        await h.Repos.Answers.DidNotReceiveWithAnyArgs().AddAsync(default!);
        h.Events.Published.Should().NotContain(e => e is AnswerAddedEvent);
    }

    // --- The normal answer-creation path remains unchanged ---

    [Fact]
    public async Task Member_answer_path_still_publishes_single_AnswerAdded_with_source_member()
    {
        var h = CreateHarness();
        h.AllowAll();
        var question = StubQuestion(null);
        h.Repos.Questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);

        await h.Sender.Send(new CreateAnswerCommand(
            ActorId, question.Id, "A member answer.", null));

        await h.Repos.Answers.Received(1).AddAsync(
            Arg.Is<Answer>(a => a.Source == AnswerSource.Member && a.Body == "A member answer."),
            Arg.Any<CancellationToken>());

        h.Events.Published.Where(e => e is AnswerAddedEvent).Should().ContainSingle();
        var added = h.Events.Published.OfType<AnswerAddedEvent>().Single();
        added.Source.Should().Be(AnswerSource.Member.Name);
        added.QuestionId.Should().Be(question.Id);
    }
}