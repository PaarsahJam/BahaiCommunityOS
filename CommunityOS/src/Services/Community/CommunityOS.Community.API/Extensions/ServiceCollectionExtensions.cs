using Asp.Versioning;
using CommunityOS.Community.API.Security;
using CommunityOS.Community.Application;
using CommunityOS.Community.Infrastructure;
using CommunityOS.Community.Infrastructure.Integration.Organization;
using CommunityOS.EventBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.Community.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddCommunityServices(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddCommunityApplication()
            .AddCommunityInfrastructure(config);

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
            .AddJwtBearer(opts => CommunityJwtValidation.Configure(opts, config));

        services.AddAuthorization();

        return services;
    }
}
