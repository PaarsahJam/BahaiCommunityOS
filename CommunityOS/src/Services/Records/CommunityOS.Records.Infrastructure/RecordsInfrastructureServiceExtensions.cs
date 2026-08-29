using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.HttpClient.Integration;
using CommunityOS.Records.Application.Abstractions;
using CommunityOS.Records.Application.Options;
using CommunityOS.Records.Domain.Repositories;
using CommunityOS.Records.Infrastructure.Integration;
using CommunityOS.Records.Infrastructure.Integration.Documents;
using CommunityOS.Records.Infrastructure.Integration.Organization;
using CommunityOS.Records.Infrastructure.Persistence;
using CommunityOS.Records.Infrastructure.Repositories;
using CommunityOS.ServiceClients;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Records.Infrastructure;

public static class RecordsInfrastructureServiceExtensions
{
    public static IServiceCollection AddRecordsInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.ConfigureRecordsOptions(config);
        services.AddDbContext<RecordsDbContext>(opts =>
            opts.UseNpgsql(
                config.GetConnectionString("RecordsDb"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(RecordsInfrastructureServiceExtensions).Assembly.FullName)));

        // Persistence
        services.AddScoped<IRecordRepository, RecordRepository>();
        services.AddScoped<IRetentionScheduleRepository, RetentionScheduleRepository>();
        services.AddScoped<IRecordCategoryRepository, RecordCategoryRepository>();
        services.AddScoped<IOrganizationUnitReferenceRepository, RecordsOrganizationUnitReferenceRepository>();

        // Organization read-model (ADR-016)
        services.AddScoped<OrganizationIntegrationEventConsumer>();

        // Documents hold/evidence reconciliation consumer (ADR-023). Requires
        // the transactional outbox (outbox gate): it must be registered only
        // when the bus is configured with the outbox (AddCommunityOSEventBusWithOutbox).
        services.AddScoped<DocumentLifecycleIntegrationEventConsumer>();

        // Domain event -> integration event forwarding onto the message bus.
        // Registered as an open generic so MediatR 12.4.1 (which dispatches by
        // the runtime type of the notification) resolves the closed publisher
        // for each concrete domain event.
        services.AddScoped(typeof(INotificationHandler<>), typeof(RecordsIntegrationEventPublisher<>));

        // Documents command surface (evidence + hold references). An
        // unconfigured base URL is a configuration error that fails closed
        // with a clear message rather than a confusing UriFormatException.
        services.AddServiceHttpClient<HttpDocumentsServiceClient, DocumentsServiceOptions>(
            config, DocumentsServiceOptions.SectionName);
        services.AddScoped<IDocumentsServiceClient>(sp => sp.GetRequiredService<HttpDocumentsServiceClient>());

        // Authorization integration: the Records service never reads the
        // Authorization database. Every decision is delegated to the
        // Authorization service's check endpoint over HTTP (ADR-018/019).
        services.AddAuthorizationHttpClient(config, "communityos-records", includeGuard: true);

        return services;
    }
}

public static class RecordsOptionsServiceExtensions
{
    /// <summary>
    /// Binds the application-level <see cref="RecordsOptions"/> from the
    /// <c>Records</c> configuration section. The Application layer is kept free
    /// of Microsoft.Extensions.Configuration so binding lives here (mirrors the
    /// Documents pattern).
    /// </summary>
    public static IServiceCollection ConfigureRecordsOptions(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<RecordsOptions>()
            .Bind(config.GetSection(RecordsOptions.SectionName));
        return services;
    }
}