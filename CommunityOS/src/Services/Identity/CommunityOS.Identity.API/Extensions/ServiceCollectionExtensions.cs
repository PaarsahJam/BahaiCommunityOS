using Asp.Versioning;
using CommunityOS.Identity.Application;
using CommunityOS.Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace CommunityOS.Identity.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddIdentityServices(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddIdentityApplication()
            .AddIdentityInfrastructure(config);

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
            .AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = config["Jwt:Issuer"],
                    ValidAudience            = config["Jwt:Audience"],
                    IssuerSigningKey         = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(config["Jwt:Secret"]!))
                };
            });

        services.AddAuthorization();

        return services;
    }
}
