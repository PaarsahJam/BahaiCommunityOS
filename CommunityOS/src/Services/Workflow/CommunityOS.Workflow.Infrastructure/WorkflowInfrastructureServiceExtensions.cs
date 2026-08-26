using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.HttpClient.Integration;
using CommunityOS.Workflow.Application.Abstractions;
using CommunityOS.Workflow.Application.Options;
using CommunityOS.Workflow.Domain.Repositories;
using CommunityOS.Workflow.Infrastructure.Integration;
using CommunityOS.Workflow.Infrastructure.Integration.Community;
using CommunityOS.Workflow.Infrastructure.Integration.Documents;
using CommunityOS.Workflow.Infrastructure.Integration.Knowledge;
using CommunityOS.Workflow.Infrastructure.Integration.Organization;
using CommunityOS.Workflow.Infrastructure.Integration.Records;
using CommunityOS.Workflow.Infrastructure.Persistence;
using CommunityOS.Workflow.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Workflow.Infrastructure;

public static class WorkflowInfrastructureServiceExtensions
{
    public static IServiceCollection AddWorkflowInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.ConfigureWorkflowOptions(config);
        services.AddDbContext<WorkflowDbContext>(opts =>
            opts.UseNpgsql(
                config.GetConnectionString("WorkflowDb"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(WorkflowInfrastructureServiceExtensions).Assembly.FullName)));

        // Persistence
        services.AddScoped<IWorkflowTaskRepository, WorkflowTaskRepository>();
        services.AddScoped<ITaskDefinitionRepository, TaskDefinitionRepository>();
        services.AddScoped<IOrganizationUnitReferenceRepository, WorkflowOrganizationUnitReferenceRepository>();

        // Organization read-model (ADR-016)
        services.AddScoped<OrganizationIntegrationEventConsumer>();

        // Records/Knowledge reconcile consumers (ADR-024). They require the
        // transactional outbox (outbox gate): registered only when the bus is
        // configured with the outbox (AddCommunityOSEventBusWithOutbox).
        services.AddScoped<RecordsReviewIntegrationEventConsumer>();
        services.AddScoped<KnowledgeReviewIntegrationEventConsumer>();

        // Domain event -> integration event forwarding onto the message bus.
        services.AddScoped(typeof(INotificationHandler<>), typeof(WorkflowIntegrationEventPublisher<>));

        // Documents command surface (task document references). An unconfigured
        // base URL is a configuration error that fails closed with a clear
        // message rather than a confusing UriFormatException.
        services.ConfigureWorkflowDocumentsService(config);
        services.AddHttpClient<HttpDocumentsServiceClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DocumentsServiceOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opts.BaseUrl))
                throw new InvalidOperationException(
                    "DocumentsService:BaseUrl is not configured; Workflow cannot create task document references.");
            client.BaseAddress = new Uri(opts.BaseUrl);
        });
        services.AddScoped<IDocumentsServiceClient>(sp => sp.GetRequiredService<HttpDocumentsServiceClient>());

        // Community command surface (assignee resolution at read time). An
        // unconfigured base URL is a configuration error that fails closed.
        services.ConfigureWorkflowCommunityService(config);
        services.AddHttpClient<HttpCommunityServiceClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CommunityServiceOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opts.BaseUrl))
                throw new InvalidOperationException(
                    "CommunityService:BaseUrl is not configured; Workflow cannot resolve assignee details.");
            client.BaseAddress = new Uri(opts.BaseUrl);
        });
        services.AddScoped<ICommunityServiceClient>(sp => sp.GetRequiredService<HttpCommunityServiceClient>());

        // Authorization integration: the Workflow service never reads the
        // Authorization database. Every decision is delegated to the
        // Authorization service's check endpoint over HTTP (ADR-018/019).
        services.AddAuthorizationHttpClient(config, "communityos-workflow", includeGuard: true);

        return services;
    }
}

public static class WorkflowOptionsServiceExtensions
{
    /// <summary>
    /// Binds the application-level <see cref="WorkflowOptions"/> from the
    /// <c>Workflow</c> configuration section. The Application layer is kept free
    /// of Microsoft.Extensions.Configuration so binding lives here.
    /// </summary>
    public static IServiceCollection ConfigureWorkflowOptions(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<WorkflowOptions>()
            .Bind(config.GetSection(WorkflowOptions.SectionName));
        return services;
    }
}