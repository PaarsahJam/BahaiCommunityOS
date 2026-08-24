using Asp.Versioning;
using CommunityOS.EventBus;
using CommunityOS.Localization.API.Security;
using CommunityOS.Localization.Application;
using CommunityOS.Localization.Infrastructure;
using CommunityOS.Localization.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.Localization.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddLocalizationServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddLocalizationApplication();
        services.AddLocalizationInfrastructure(config);

        // Born with the producer outbox (ADR-015; ADR-029 decisions 14/16).
        // ZERO consumers at the ratified first gate — configureConsumers is
        // deliberately omitted; the inbox is modeled but inert. Consumption by
        // Audit would require the recorded ADR-027 amendment.
        services.AddCommunityOSEventBusWithOutbox<LocalizationDbContext>(config);

        services.AddControllers()
            .AddJsonOptions(x => x.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
        services.AddApiVersioning(x =>
        {
            x.DefaultApiVersion = new ApiVersion(1, 0);
            x.AssumeDefaultVersionWhenUnspecified = true;
            x.ReportApiVersions = true;
        }).AddApiExplorer(x => { x.GroupNameFormat = "'v'VVV"; x.SubstituteApiVersionInUrl = true; });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(x => LocalizationJwtValidation.Configure(x, config));
        services.AddAuthorization();
        return services;
    }
}
