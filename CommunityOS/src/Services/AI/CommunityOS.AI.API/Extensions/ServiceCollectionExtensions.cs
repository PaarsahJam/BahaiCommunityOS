using Asp.Versioning;
using CommunityOS.AI.API.Security;
using CommunityOS.AI.Application;
using CommunityOS.AI.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.AI.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddAiApplication();
        services.AddAiInfrastructure();

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
            .AddJwtBearer(x => AiJwtValidation.Configure(x, config));
        services.AddAuthorization();

        return services;
    }
}
