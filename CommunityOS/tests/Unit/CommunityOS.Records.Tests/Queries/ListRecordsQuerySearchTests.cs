using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Records.Application;
using CommunityOS.Records.Application.Abstractions;
using CommunityOS.Records.Application.Queries;
using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CommunityOS.Records.Tests.Queries;

/// <summary>
/// Regression tests for the <c>query=</c> free-text list search on
/// <c>GET /api/v1/records</c> (ADR-023; documented in <c>docs/api/records.md</c>).
/// The search is a case-insensitive substring match over the record's current
/// non-sensitive field values. Sensitive field values are never searched, so a
/// list search cannot reveal sensitive data.
/// </summary>
public class ListRecordsQuerySearchTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid OtherActorId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private sealed class Harness
    {
        private readonly IRecordRepository _records = Substitute.For<IRecordRepository>();
        private readonly IAuthorizationEvaluator _evaluator = Substitute.For<IAuthorizationEvaluator>();

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddRecordsApplication();
            services.AddScoped(_ => _records);
            services.AddScoped(_ => Substitute.For<IRecordCategoryRepository>());
            services.AddScoped(_ => Substitute.For<IRetentionScheduleRepository>());
            services.AddScoped(_ => Substitute.For<IOrganizationUnitReferenceRepository>());
            services.AddScoped(_ => _evaluator);
            services.AddScoped(_ => Substitute.For<IDocumentsServiceClient>());
            Provider = services.BuildServiceProvider();
        }

        public ServiceProvider Provider { get; }

        public ISender Sender => Provider.GetRequiredService<ISender>();

        public void Seed(params Record[] records)
        {
            _records.ListAsync(Arg.Any<CancellationToken>()).Returns(records.ToList());
        }

        public void AllowRead(params Guid[] recordIds)
        {
            _evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var r = call.Arg<AuthorizationRequest>();
                    return (r.Context.ResourceId is null || recordIds.Contains(r.Context.ResourceId.Value))
                        ? AuthorizationDecision.Allow("allow", [r.Permission], DateTime.UtcNow)
                        : AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow);
                });

            _evaluator.EvaluateBatchAsync(
                    Arg.Any<IReadOnlyList<AuthorizationRequest>>(), Arg.Any<CancellationToken>())
                .Returns(call => call.Arg<IReadOnlyList<AuthorizationRequest>>()
                    .Select(r => recordIds.Contains(r.Context.ResourceId ?? Guid.Empty)
                        ? AuthorizationDecision.Allow("allow", [r.Permission], DateTime.UtcNow)
                        : AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow))
                    .ToList());
        }
    }

    private static Record StubRecord(string name, bool sensitive)
    {
        var record = Record.Create("membership", "person", Guid.NewGuid(), Guid.NewGuid(),
            [RecordFieldValue.Create("name", name, sensitive)], sensitive, ActorId, Now);
        return record;
    }

    [Fact]
    public async Task Query_filters_by_non_sensitive_field_value_case_insensitively()
    {
        var h = new Harness();
        var match = StubRecord("Bahá'í Community", sensitive: false);
        var other = StubRecord("Youth Group", sensitive: false);
        h.Seed(match, other);
        h.AllowRead(match.Id, other.Id);

        var result = await h.Sender.Send(new ListRecordsQuery(
            ActorId, null, null, null, null, null, null, "COMMUNITY"));

        result.Should().ContainSingle();
        result.Single().Id.Should().Be(match.Id);
    }

    [Fact]
    public async Task Query_never_matches_sensitive_field_values()
    {
        var h = new Harness();
        var sensitiveMatch = Record.Create("membership", "person", Guid.NewGuid(), Guid.NewGuid(),
            [
                RecordFieldValue.Create("name", "Public Record", false),
                RecordFieldValue.Create("alias", "CLASSIFIED ALIAS", true)
            ], isSensitive: false, ActorId, Now);
        var nonSensitiveMatch = StubRecord("alias announcement", sensitive: false);
        h.Seed(sensitiveMatch, nonSensitiveMatch);
        h.AllowRead(sensitiveMatch.Id, nonSensitiveMatch.Id);

        var result = await h.Sender.Send(new ListRecordsQuery(
            ActorId, null, null, null, null, null, null, "alias"));

        // The record matching only through its sensitive field is never returned:
        // the search cannot reveal sensitive data through the non-sensitive list.
        result.Should().ContainSingle();
        result.Single().Id.Should().Be(nonSensitiveMatch.Id);
    }

    [Fact]
    public async Task Null_or_whitespace_query_returns_all_readable_records()
    {
        var h = new Harness();
        var a = StubRecord("Alpha", sensitive: false);
        var b = StubRecord("Beta", sensitive: false);
        h.Seed(a, b);
        h.AllowRead(a.Id, b.Id);

        var result = await h.Sender.Send(new ListRecordsQuery(
            ActorId, null, null, null, null, null, null, "   "));

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Query_matches_the_current_verified_version_not_stale_working_fields()
    {
        var h = new Harness();
        var record = Record.Create("membership", "person", Guid.NewGuid(), Guid.NewGuid(),
            [RecordFieldValue.Create("name", "Old Value", false)], isSensitive: false, ActorId, Now);
        record.Submit(ActorId, Now);
        record.MoveUnderReview(OtherActorId, Now);
        record.Verify(OtherActorId, Now);
        record.Correct(
            [RecordFieldValue.Create("name", "New Value", false)],
            "Correction", OtherActorId, Now);
        h.Seed(record);
        h.AllowRead(record.Id);

        var oldResult = await h.Sender.Send(new ListRecordsQuery(
            ActorId, null, null, null, null, null, null, "old"));
        oldResult.Should().BeEmpty();

        var newResult = await h.Sender.Send(new ListRecordsQuery(
            ActorId, null, null, null, null, null, null, "new"));
        newResult.Should().ContainSingle();
        newResult.Single().Id.Should().Be(record.Id);
    }
}