using Asp.Versioning;
using CommunityOS.Correspondence.API.Security;
using CommunityOS.Correspondence.Application;
using CommunityOS.Correspondence.Infrastructure;
using CommunityOS.Correspondence.Infrastructure.Integration;
using CommunityOS.Correspondence.Infrastructure.Persistence;
using CommunityOS.EventBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.Correspondence.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddCorrespondenceServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddCorrespondenceApplication();
        services.AddCorrespondenceInfrastructure(config);

        // Born with the producer outbox (ADR-015). Exactly one consumer at
        // this gate: the local organization-unit projection (ADR-016). The
        // Documents consumer is deliberately NOT registered — it opens only
        // with the producer outbox upgrade on the Documents side (ADR-028).
        services.AddCommunityOSEventBusWithOutbox<CorrespondenceDbContext>(config, bus =>
        {
            bus.AddConsumer<OrganizationUnitProjectionConsumer>();
        });

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
            .AddJwtBearer(x => CorrespondenceJwtValidation.Configure(x, config));
        services.AddAuthorization();
        return services;
    }
}
