using CommunityOS.Documents.API.Config;
using CommunityOS.Documents.API.Security;
using CommunityOS.Documents.Application;
using CommunityOS.Documents.Application.Options;
using CommunityOS.Documents.Infrastructure;
using CommunityOS.Documents.Infrastructure.Integration.Organization;
using CommunityOS.EventBus;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.Documents.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddDocumentsServices(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddDocumentsApplication()
            .AddDocumentsInfrastructure(config);

        services.AddOptions<DocumentsApiOptions>()
            .Bind(config.GetSection(DocumentsApiOptions.SectionName));

        services.AddOptions<DocumentsIntegrityOptions>()
            .Bind(config.GetSection(DocumentsIntegrityOptions.SectionName));

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
            .AddJwtBearer(opts => DocumentsJwtValidation.Configure(opts, config));

        services.AddAuthorization();

        return services;
    }
}