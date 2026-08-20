using CommunityOS.Workflow.API.Config;
using CommunityOS.Workflow.API.Security;
using CommunityOS.Workflow.Application;
using CommunityOS.Workflow.Infrastructure;
using CommunityOS.Workflow.Infrastructure.Integration.Knowledge;
using CommunityOS.Workflow.Infrastructure.Integration.Organization;
using CommunityOS.Workflow.Infrastructure.Integration.Records;
using CommunityOS.Workflow.Infrastructure.Persistence;
using CommunityOS.EventBus;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.Workflow.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddWorkflowServices(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddWorkflowApplication()
            .AddWorkflowInfrastructure(config);

        services.AddOptions<WorkflowApiOptions>()
            .Bind(config.GetSection(WorkflowApiOptions.SectionName));

        // Workflow is itself a guaranteed-delivery consumer of Records and
        // Knowledge events, and its own compliance-significant events target
        // Notifications/Search/Audit. The transactional outbox (ADR-015) is
        // enabled at the Workflow integration gate — best-effort publication is
        // never acceptable (docs/runbooks/workflow.md). Consumers registered
        // here use the receive-endpoint outbox to consume exactly-once; the
        // same DbContext backs both the domain data and the outbox tables so
        // the commit is atomic.
        services.AddCommunityOSEventBusWithOutbox<WorkflowDbContext>(
            config,
            bus =>
            {
                bus.AddConsumer<OrganizationIntegrationEventConsumer>();
                bus.AddConsumer<RecordsReviewIntegrationEventConsumer>();
                bus.AddConsumer<KnowledgeReviewIntegrationEventConsumer>();
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
            .AddJwtBearer(opts => WorkflowJwtValidation.Configure(opts, config));

        services.AddAuthorization();

        return services;
    }
}