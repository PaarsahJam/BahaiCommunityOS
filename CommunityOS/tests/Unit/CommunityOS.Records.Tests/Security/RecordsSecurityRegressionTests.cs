using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.Domain.Exceptions;
using CommunityOS.Records.Application;
using CommunityOS.Records.Application.Abstractions;
using CommunityOS.Records.Application.Commands;
using CommunityOS.Records.Application.Permissions;
using CommunityOS.Records.Application.Queries;
using CommunityOS.Records.Domain.Aggregates;
using CommunityOS.Records.Domain.Exceptions;
using CommunityOS.Records.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CommunityOS.Records.Tests.Security;

/// <summary>
/// Security regression tests for the Records bounded context (ADR-023). These
/// lock in the authorization boundaries: every command and guarded query flows
/// through the Authorization guard, the guard is fail-closed (a denied or
/// unreachable evaluator blocks the operation and nothing is persisted and no
/// Documents side effect occurs), metadata and sensitive-field access are
/// separate capabilities, and denied reads are indistinguishable from
/// not-found (no enumeration oracle).
/// </summary>
public class RecordsSecurityRegressionTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private sealed class Harness
    {
        public RepoSet Repos { get; } = RepoSet.Create();
        public ServiceProvider Provider { get; }

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
            services.AddRecordsApplication();

            services.AddScoped(_ => Repos.Records);
            services.AddScoped(_ => Repos.Categories);
            services.AddScoped(_ => Repos.Retention);
            services.AddScoped(_ => Repos.Units);
            services.AddScoped(_ => Repos.Evaluator);
            services.AddScoped(_ => Repos.Documents);

            Provider = services.BuildServiceProvider();
        }

        public ISender Sender => Provider.GetRequiredService<ISender>();

        public void AllowKnownCatalog()
        {
            Repos.Categories.ExistsByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(true);
            Repos.Units.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(true);
        }

        /// <summary>Denies every authorization request (fail-closed default).</summary>
        public void DenyAll() => Repos.Evaluator.EvaluateAsync(
                Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow));

        /// <summary>Allows a single permission, optionally scoped to a resource.</summary>
        public void Allow(string permission, Guid? resourceId = null, Guid? unitId = null) =>
            Repos.Evaluator.EvaluateAsync(
                    Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var r = call.Arg<AuthorizationRequest>();
                    if (r.Permission != permission) return Deny();
                    if (resourceId is { } id && r.Context.ResourceId != id) return Deny();
                    if (unitId is { } u && r.Context.OrganizationUnitId != u) return Deny();
                    return AuthorizationDecision.Allow("allow", [r.Permission], DateTime.UtcNow);
                });

        /// <summary>
        /// Fail-closed batch evaluation used by list filtering: the coarse
        /// scope-level read (resourceId is null) is granted, but every
        /// per-record batch check is allowed only for the listed resources.
        /// </summary>
        public void AllowBatchOnly(params Guid[] allowedResourceIds)
        {
            Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var r = call.Arg<AuthorizationRequest>();
                    return (r.Context.ResourceId is null ||
                            allowedResourceIds.Contains(r.Context.ResourceId.Value))
                        ? AuthorizationDecision.Allow("allow", [r.Permission], DateTime.UtcNow)
                        : Deny();
                });

            Repos.Evaluator.EvaluateBatchAsync(
                    Arg.Any<IReadOnlyList<AuthorizationRequest>>(), Arg.Any<CancellationToken>())
                .Returns(call => call.Arg<IReadOnlyList<AuthorizationRequest>>()
                    .Select(r => allowedResourceIds.Contains(r.Context.ResourceId ?? Guid.Empty)
                        ? AuthorizationDecision.Allow("allow", [r.Permission], DateTime.UtcNow)
                        : Deny())
                    .ToList());
        }

        /// <summary>Allows the coarse read plus the sensitive capability for the resource.</summary>
        public void AllowSensitiveRead(Guid resourceId)
        {
            Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var r = call.Arg<AuthorizationRequest>();
                    if (r.Context.ResourceId != resourceId) return Deny();
                    if (r.Permission is not (RecordsPermissions.RecordRead or RecordsPermissions.RecordReadSensitive))
                        return Deny();
                    return AuthorizationDecision.Allow("allow", [r.Permission], DateTime.UtcNow);
                });
        }

        private static AuthorizationDecision Deny() =>
            AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.NoPermission, DateTime.UtcNow);
    }

    private sealed record RepoSet(
        IRecordRepository Records,
        IRecordCategoryRepository Categories,
        IRetentionScheduleRepository Retention,
        IOrganizationUnitReferenceRepository Units,
        IAuthorizationEvaluator Evaluator,
        IDocumentsServiceClient Documents)
    {
        public static RepoSet Create() => new(
            Substitute.For<IRecordRepository>(),
            Substitute.For<IRecordCategoryRepository>(),
            Substitute.For<IRetentionScheduleRepository>(),
            Substitute.For<IOrganizationUnitReferenceRepository>(),
            Substitute.For<IAuthorizationEvaluator>(),
            Substitute.For<IDocumentsServiceClient>());
    }

    private static Harness CreateHarness() => new();

    private static Record StubRecord(Guid? unitId, bool sensitive = false) =>
        Record.Create("membership", "person", Guid.NewGuid(), unitId,
            [RecordFieldValue.Create("name", "A Flower", sensitive)], sensitive, ActorId, Now);

    // --- 1. Denied operation fails and persists nothing ---

    [Fact]
    public async Task CreateRecord_is_denied_and_nothing_is_persisted_when_evaluator_denies()
    {
        var h = CreateHarness();
        h.DenyAll();
        h.AllowKnownCatalog();

        var act = () => h.Sender.Send(new CreateRecordCommand(
            ActorId, "membership", "person", Guid.NewGuid(), null,
            [RecordFieldValue.Create("name", "A Flower", false)], IsSensitive: false));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Records.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    // --- 2. Unavailable evaluator fails closed ---

    [Fact]
    public async Task CreateRecord_is_denied_when_evaluator_is_unreachable()
    {
        var h = CreateHarness();
        h.AllowKnownCatalog();
        h.Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AuthorizationDecision>(new HttpRequestException("authz down")));

        var act = () => h.Sender.Send(new CreateRecordCommand(
            ActorId, "membership", "person", Guid.NewGuid(), null,
            [RecordFieldValue.Create("name", "A Flower", false)], IsSensitive: false));

        await act.Should().ThrowAsync<Exception>();
        await h.Repos.Records.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    // --- 3. Missing subject is denied (fail-closed) ---

    [Fact]
    public async Task Missing_subject_is_denied_by_the_guard()
    {
        var evaluator = Substitute.For<IAuthorizationEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
                call.Arg<AuthorizationRequest>().SubjectId == Guid.Empty
                    ? AuthorizationDecision.Deny("d", AuthorizationDecisionReason.MissingSubject, DateTime.UtcNow)
                    : AuthorizationDecision.Allow("a", [RecordsPermissions.RecordCreate], DateTime.UtcNow));

        var guard = new AuthorizationGuard(evaluator);

        var act = () => guard.RequireAsync(Guid.Empty, RecordsPermissions.RecordCreate,
            new AuthorizationContext(ResourceType: "record"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
    }

    // --- 4. Denied reads are indistinguishable from not-found ---

    [Fact]
    public async Task GetRecord_throws_RecordNotFound_when_read_is_denied()
    {
        var h = CreateHarness();
        var record = StubRecord(Guid.NewGuid());
        h.Repos.Records.GetByIdAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(record);
        h.DenyAll();

        var act = () => h.Sender.Send(new GetRecordQuery(ActorId, record.Id));

        await act.Should().ThrowAsync<RecordNotFoundException>();
    }

    // --- 5. List filtering is fail-closed: unauthorized records are excluded ---

    [Fact]
    public async Task ListRecords_returns_only_records_the_caller_may_read()
    {
        var h = CreateHarness();
        var allowed = StubRecord(Guid.NewGuid());
        var denied = StubRecord(Guid.NewGuid());
        h.Repos.Records.ListAsync(Arg.Any<CancellationToken>())
            .Returns([allowed, denied]);
        h.AllowBatchOnly(allowed.Id);

        var result = await h.Sender.Send(new ListRecordsQuery(
            ActorId, null, null, null, null, null, null, null));

        result.Should().ContainSingle();
        result.Single().Id.Should().Be(allowed.Id);
    }

    // --- 6. Sensitive fields require the extra sensitive capability ---

    [Fact]
    public async Task Sensitive_fields_are_not_returned_without_the_sensitive_capability()
    {
        var h = CreateHarness();
        var record = StubRecord(Guid.NewGuid(), sensitive: true);
        record.Submit(ActorId, Now);
        record.MoveUnderReview(Guid.NewGuid(), Now);
        record.Verify(Guid.NewGuid(), Now);
        h.Repos.Records.GetByIdAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(record);
        // Coarse + per-record read allowed, but NOT the sensitive capability.
        h.Allow(RecordsPermissions.RecordRead, resourceId: record.Id);

        var act = () => h.Sender.Send(new GetRecordSensitiveFieldsQuery(ActorId, record.Id));

        await act.Should().ThrowAsync<RecordNotFoundException>();
    }

    [Fact]
    public async Task Sensitive_fields_are_returned_with_the_sensitive_capability()
    {
        var h = CreateHarness();
        var record = StubRecord(Guid.NewGuid(), sensitive: true);
        record.Submit(ActorId, Now);
        record.MoveUnderReview(Guid.NewGuid(), Now);
        record.Verify(Guid.NewGuid(), Now);
        h.Repos.Records.GetByIdAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(record);
        h.AllowSensitiveRead(record.Id);

        var result = await h.Sender.Send(new GetRecordSensitiveFieldsQuery(ActorId, record.Id));

        result.Should().ContainSingle(f => f.FieldKey == "name" && f.IsSensitive);
    }

    // --- 7. Multi-scope: access is granted at ANY organization scope ---

    [Fact]
    public async Task Read_is_granted_via_an_additional_scope_when_the_primary_scope_is_denied()
    {
        var h = CreateHarness();
        var primary = Guid.NewGuid();
        var additional = Guid.NewGuid();
        var record = StubRecord(primary);
        record.AddOrganizationScope(additional, ActorId, Now);
        h.Repos.Records.GetByIdAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(record);
        // The caller holds read only at the ADDITIONAL scope, never the primary.
        h.Allow(RecordsPermissions.RecordRead, resourceId: record.Id, unitId: additional);

        var result = await h.Sender.Send(new GetRecordQuery(ActorId, record.Id));

        result.Id.Should().Be(record.Id);
    }

    [Fact]
    public async Task Read_is_denied_when_the_caller_has_the_permission_at_neither_scope()
    {
        var h = CreateHarness();
        var record = StubRecord(Guid.NewGuid());
        record.AddOrganizationScope(Guid.NewGuid(), ActorId, Now);
        h.Repos.Records.GetByIdAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(record);
        // The caller holds read at an unrelated scope only — never any of the record's scopes.
        h.Allow(RecordsPermissions.RecordRead, resourceId: record.Id, unitId: Guid.NewGuid());

        var act = () => h.Sender.Send(new GetRecordQuery(ActorId, record.Id));

        await act.Should().ThrowAsync<RecordNotFoundException>();
    }

    [Fact]
    public async Task Update_is_granted_via_an_additional_scope_when_the_primary_scope_is_denied()
    {
        var h = CreateHarness();
        var primary = Guid.NewGuid();
        var additional = Guid.NewGuid();
        var record = StubRecord(primary);
        record.AddOrganizationScope(additional, ActorId, Now);
        h.Repos.Records.GetByIdAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(record);
        h.Allow(RecordsPermissions.RecordUpdate, resourceId: record.Id, unitId: additional);

        var result = await h.Sender.Send(new UpdateRecordFieldsCommand(
            ActorId, record.Id, [RecordFieldValue.Create("name", "Updated", false)]));

        result.Id.Should().Be(record.Id);
        record.WorkingFields.Should().Contain(f => f.FieldKey == "name" && f.FieldValue == "Updated");
        await h.Repos.Records.Received(1).UpdateAsync(record, Arg.Any<CancellationToken>());
    }

    // --- 8. Denied commands never persist ---

    [Fact]
    public async Task Update_is_not_persisted_when_authorization_is_denied()
    {
        var h = CreateHarness();
        var record = StubRecord(Guid.NewGuid());
        h.Repos.Records.GetByIdAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(record);
        h.DenyAll();

        var act = () => h.Sender.Send(new UpdateRecordFieldsCommand(
            ActorId, record.Id, [RecordFieldValue.Create("name", "Updated", false)]));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Records.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task Verify_is_not_persisted_when_authorization_is_denied()
    {
        var h = CreateHarness();
        var record = StubRecord(Guid.NewGuid());
        h.Repos.Records.GetByIdAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(record);
        h.DenyAll();

        var act = () => h.Sender.Send(new VerifyRecordCommand(ActorId, record.Id));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Records.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task Deactivate_is_not_persisted_when_authorization_is_denied()
    {
        var h = CreateHarness();
        var record = StubRecord(Guid.NewGuid());
        h.Repos.Records.GetByIdAsync(record.Id, Arg.Any<CancellationToken>())
            .Returns(record);
        h.DenyAll();

        var act = () => h.Sender.Send(new DeactivateRecordCommand(ActorId, record.Id, AdminOverride: false, Reason: null));

        await act.Should().ThrowAsync<AuthorizationForbiddenException>();
        await h.Repos.Records.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }
}