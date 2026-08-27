using CommunityOS.Finance.API.Config;
using CommunityOS.Finance.API.Security;
using CommunityOS.Finance.Application;
using CommunityOS.Finance.Infrastructure;
using CommunityOS.Finance.Infrastructure.Integration.Organization;
using CommunityOS.Finance.Infrastructure.Persistence;
using CommunityOS.EventBus;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.Finance.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddFinanceServices(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddFinanceApplication()
            .AddFinanceInfrastructure(config);

        services.AddOptions<FinanceApiOptions>()
            .Bind(config.GetSection(FinanceApiOptions.SectionName));

        // The transactional outbox (ADR-015) is enabled here just as in the
        // Records service: the Finance DbContext backs both the ledger and the
        // outbox tables, so the FinanceTransactionRecorded contract and the
        // ledger row commit atomically. The outbox is also required for the
        // ingest of Organization unit events.
        services.AddCommunityOSEventBusWithOutbox<FinanceDbContext>(
            config,
            bus =>
            {
                bus.AddConsumer<OrganizationIntegrationEventConsumer>();
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
            .AddJwtBearer(opts => FinanceJwtValidation.Configure(opts, config));

        services.AddAuthorization();

        return services;
    }
}