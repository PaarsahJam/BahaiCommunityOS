using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Notifications.Application.Options;
using CommunityOS.Notifications.Domain.Repositories;
using CommunityOS.Notifications.Infrastructure.Background;
using CommunityOS.Notifications.Infrastructure.Integration;
using CommunityOS.Notifications.Infrastructure.Integration.Authorization;
using CommunityOS.Notifications.Infrastructure.Integration.Community;
using CommunityOS.Notifications.Infrastructure.Integration.Organization;
using CommunityOS.Notifications.Infrastructure.Integration.Workflow;
using CommunityOS.Notifications.Infrastructure.Persistence;
using CommunityOS.Notifications.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Notifications.Infrastructure;

public static class NotificationsInfrastructureServiceExtensions
{
    public static IServiceCollection AddNotificationsInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.ConfigureNotificationsOptions(config);
        services.AddDbContext<NotificationsDbContext>(opts =>
            opts.UseNpgsql(
                config.GetConnectionString("NotificationsDb"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(NotificationsInfrastructureServiceExtensions).Assembly.FullName)));

        // Persistence
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTypeRepository, NotificationTypeRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<IOrganizationUnitReferenceRepository, NotificationsOrganizationUnitReferenceRepository>();

        // Organization read-model (ADR-016)
        services.AddScoped<OrganizationIntegrationEventConsumer>();

        // Workflow reconcile consumer (ADR-024 → ADR-025, first gate). It
        // requires the transactional outbox (outbox gate): registered only when
        // the bus is configured with the outbox (AddCommunityOSEventBusWithOutbox).
        services.AddScoped<WorkflowTaskIntegrationEventConsumer>();

        // Domain event -> integration event forwarding onto the message bus.
        services.AddScoped(typeof(INotificationHandler<>), typeof(NotificationsIntegrationEventPublisher<>));

        // Dispatch worker: polls queued notifications and drives dispatch.
        services.AddHostedService<NotificationDispatchWorker>();

        // Authorization integration: the Notifications service never reads the
        // Authorization database. Every decision is delegated to the
        // Authorization service's check endpoint over HTTP (ADR-018/019).
        services.ConfigureNotificationsAuthorizationService(config);
        services.AddHttpClient<HttpAuthorizationEvaluator>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationServiceOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
        });
        services.AddScoped<IAuthorizationEvaluator>(sp => sp.GetRequiredService<HttpAuthorizationEvaluator>());
        services.AddScoped<AuthorizationGuard>();

        // Community configuration is reserved for future channel-destination
        // resolution at dispatch time (ADR-025 decision 5). InApp needs no
        // destination resolution; Email/SMS/Push fail closed until a provider
        // integration exists, so no HTTP client is registered in the first gate.
        services.ConfigureNotificationsCommunityService(config);

        return services;
    }
}

public static class NotificationsOptionsServiceExtensions
{
    /// <summary>
    /// Binds the application-level <see cref="NotificationsOptions"/> from the
    /// <c>Notifications</c> configuration section. The Application layer is kept
    /// free of Microsoft.Extensions.Configuration so binding lives here.
    /// </summary>
    public static IServiceCollection ConfigureNotificationsOptions(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<NotificationsOptions>()
            .Bind(config.GetSection(NotificationsOptions.SectionName));
        return services;
    }
}