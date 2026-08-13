using CommunityOS.Knowledge.API.Security;
using CommunityOS.Knowledge.Application;
using CommunityOS.Knowledge.Infrastructure;
using CommunityOS.Knowledge.Infrastructure.Integration.Organization;
using CommunityOS.EventBus;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.Knowledge.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddKnowledgeServices(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddKnowledgeApplication()
            .AddKnowledgeInfrastructure(config);

        services.AddCommunityOSEventBus(
            config,
            bus => bus.AddConsumer<OrganizationIntegrationEventConsumer>());

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
            .AddJwtBearer(opts => KnowledgeJwtValidation.Configure(opts, config));

        services.AddAuthorization();

        return services;
    }
}