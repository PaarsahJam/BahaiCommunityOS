using CommunityOS.Notifications.API.Config;
using CommunityOS.Notifications.API.Security;
using CommunityOS.Notifications.Application;
using CommunityOS.Notifications.Infrastructure;
using CommunityOS.Notifications.Infrastructure.Integration.Organization;
using CommunityOS.Notifications.Infrastructure.Integration.Workflow;
using CommunityOS.Notifications.Infrastructure.Persistence;
using CommunityOS.EventBus;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.Notifications.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddNotificationsServices(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddNotificationsApplication()
            .AddNotificationsInfrastructure(config);

        services.AddOptions<NotificationsApiOptions>()
            .Bind(config.GetSection(NotificationsApiOptions.SectionName));

        // Notifications is itself a guaranteed-delivery consumer of Organization
        // and Workflow events, and its own compliance-significant
        // NotificationDispatched event targets Audit (slot 11) and Analytics
        // (slot 19). The transactional outbox (ADR-015) is enabled at the
        // Notifications integration gate — best-effort publication is never
        // acceptable (docs/runbooks/notifications.md). Consumers registered here
        // use the receive-endpoint outbox to consume exactly-once; the same
        // DbContext backs both the domain data and the outbox tables so the
        // commit is atomic.
        services.AddCommunityOSEventBusWithOutbox<NotificationsDbContext>(
            config,
            bus =>
            {
                bus.AddConsumer<OrganizationIntegrationEventConsumer>();
                bus.AddConsumer<WorkflowTaskIntegrationEventConsumer>();
            });

        services
            .AddControllers()
            .AddJsonOptions(opts =>
                opts.JsonSerializerOptions.PropertyNamingPolicy =
                    System.Text.Json.JsonNamingPolicy.CamelCase);

        services.AddApiVersioning(opts =>
        {
            opts.DefaultApiVersion = new ApiVersion(1, 0);
            opts.AssumeDefaultVersionWhenUnspecified = true;
            opts.ReportApiVersions = true;
        }).AddApiExplorer(opts =>
        {
            opts.GroupNameFormat = "'v'VVV";
            opts.SubstituteApiVersionInUrl = true;
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts => NotificationsJwtValidation.Configure(opts, config));

        services.AddAuthorization();

        return services;
    }
}