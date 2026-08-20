using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Notifications.Application;
using CommunityOS.Notifications.Application.Services;
using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.Entities;
using CommunityOS.Notifications.Domain.Repositories;
using CommunityOS.Notifications.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CommunityOS.Notifications.Tests.Application;

/// <summary>
/// In-memory MediatR harness with substitute repositories and an
/// <see cref="IAuthorizationEvaluator"/> that decides per permission (fail closed
/// by default). Models the Documents/Workflow application-test convention: the
/// real Application DI wiring (MediatR, validation pipeline, AuthorizationGuard,
/// dispatch service) runs against substitute ports.
/// </summary>
internal sealed class ApplicationHarness
{
    public RepoSet Repos { get; } = RepoSet.Create();
    public ServiceProvider Provider { get; }

    public ApplicationHarness()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddNotificationsApplication();

        services.AddScoped(_ => Repos.Notifications);
        services.AddScoped(_ => Repos.Types);
        services.AddScoped(_ => Repos.Preferences);
        services.AddScoped(_ => Repos.Units);
        services.AddScoped<IAuthorizationEvaluator>(_ => Repos.Evaluator);

        // Defaults so un-stubbed async port methods never return null tasks.
        Repos.Preferences.ListDisabledAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        Repos.Preferences.ListByMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        Provider = services.BuildServiceProvider();
    }

    public ISender Sender => Provider.GetRequiredService<ISender>();

    public AuthorizationGuard Guard => Provider.GetRequiredService<AuthorizationGuard>();

    public NotificationDispatchService DispatchService => Provider.GetRequiredService<NotificationDispatchService>();

    /// <summary>Allows the given permission at every scope (organization or global).</summary>
    public void Allow(string permission) =>
        Repos.Evaluator.EvaluateAsync(
                Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
                PermissionMatches(call.Arg<AuthorizationRequest>(), permission)
                    ? AuthorizationDecision.Allow("allow", [permission], DateTime.UtcNow)
                    : AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.DeniedByDefault, DateTime.UtcNow));

    public void AllowBatch(params string[] permissions)
    {
        var allowed = new HashSet<string>(permissions, StringComparer.Ordinal);
        Repos.Evaluator.EvaluateBatchAsync(
                Arg.Any<IReadOnlyList<AuthorizationRequest>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IReadOnlyList<AuthorizationRequest>>()
                .Select(r => allowed.Contains(r.Permission)
                    ? AuthorizationDecision.Allow("allow", [r.Permission], DateTime.UtcNow)
                    : AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.DeniedByDefault, DateTime.UtcNow))
                .ToList()
                .AsReadOnly());
        Repos.Evaluator.EvaluateAsync(
                Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => allowed.Contains(call.Arg<AuthorizationRequest>().Permission)
                ? AuthorizationDecision.Allow("allow", [call.Arg<AuthorizationRequest>().Permission], DateTime.UtcNow)
                : AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.DeniedByDefault, DateTime.UtcNow));
    }

    public void DenyAll()
    {
        Repos.Evaluator.EvaluateAsync(Arg.Any<AuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(AuthorizationDecision.Deny("deny", AuthorizationDecisionReason.DeniedByDefault, DateTime.UtcNow));
        Repos.Evaluator.EvaluateBatchAsync(Arg.Any<IReadOnlyList<AuthorizationRequest>>(), Arg.Any<CancellationToken>())
            .Returns(_ => new List<AuthorizationDecision>());
    }

    private static bool PermissionMatches(AuthorizationRequest request, string permission) =>
        string.Equals(request.Permission, permission, StringComparison.Ordinal);

    public static Notification CreateQueuedNotification(
        Guid? sourceId = null,
        IReadOnlyList<Guid>? recipients = null,
        bool isSensitive = false,
        NotificationChannel? channel = null,
        Guid? organizationUnitId = null)
    {
        var notification = Notification.Create(
            "general",
            channel ?? NotificationChannel.InApp,
            sourceId is null ? null : "workflow-task",
            sourceId,
            MessageTemplate.Create("Subject", "Body"),
            organizationUnitId,
            [],
            scheduledFor: null,
            isSensitive,
            recipients ?? [Guid.NewGuid()],
            Guid.NewGuid(),
            DateTime.UtcNow);
        notification.Queue();
        return notification;
    }

    public static NotificationType CreateType(string code = "general", bool isSensitive = false) =>
        NotificationType.Create(
            code, code, NotificationChannel.InApp,
            "Subject {{Var}}", "Body {{Var}}", isSensitive, Guid.NewGuid(), DateTime.UtcNow);

    internal sealed record RepoSet(
        INotificationRepository Notifications,
        INotificationTypeRepository Types,
        INotificationPreferenceRepository Preferences,
        IOrganizationUnitReferenceRepository Units,
        IAuthorizationEvaluator Evaluator)
    {
        public static RepoSet Create() => new(
            Substitute.For<INotificationRepository>(),
            Substitute.For<INotificationTypeRepository>(),
            Substitute.For<INotificationPreferenceRepository>(),
            Substitute.For<IOrganizationUnitReferenceRepository>(),
            Substitute.For<IAuthorizationEvaluator>());
    }
}

internal static class ApplicationTestData
{
    public static readonly Guid Actor = Guid.NewGuid();
    public static readonly Guid Recipient = Guid.NewGuid();
    public static readonly DateTime Now = DateTime.UtcNow;
}