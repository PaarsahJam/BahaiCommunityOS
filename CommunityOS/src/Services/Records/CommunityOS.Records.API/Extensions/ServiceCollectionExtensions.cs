using CommunityOS.Records.API.Config;
using CommunityOS.Records.API.Security;
using CommunityOS.Records.Application;
using CommunityOS.Records.Infrastructure;
using CommunityOS.Records.Infrastructure.Integration.Documents;
using CommunityOS.Records.Infrastructure.Integration.Organization;
using CommunityOS.Records.Infrastructure.Persistence;
using CommunityOS.EventBus;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.Records.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddRecordsServices(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddRecordsApplication()
            .AddRecordsInfrastructure(config);

        services.AddOptions<RecordsApiOptions>()
            .Bind(config.GetSection(RecordsApiOptions.SectionName));

        // Records is the first service with guaranteed-delivery consumers
        // (ADR-023), so the transactional outbox (ADR-015) is enabled here.
        // Consumers registered here must use the receive-endpoint outbox to
        // consume exactly-once; the same DbContext backs both the domain data
        // and the outbox tables so the commit is atomic.
        services.AddCommunityOSEventBusWithOutbox<RecordsDbContext>(
            config,
            bus =>
            {
                bus.AddConsumer<OrganizationIntegrationEventConsumer>();
                bus.AddConsumer<DocumentLifecycleIntegrationEventConsumer>();
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
            .AddJwtBearer(opts => RecordsJwtValidation.Configure(opts, config));

        services.AddAuthorization();

        return services;
    }
}