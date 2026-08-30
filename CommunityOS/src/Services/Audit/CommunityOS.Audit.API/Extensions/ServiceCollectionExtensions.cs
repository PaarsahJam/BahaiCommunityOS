using Asp.Versioning;
using CommunityOS.Audit.API.Security;
using CommunityOS.Audit.Application;
using CommunityOS.Audit.Infrastructure;
using CommunityOS.Audit.Infrastructure.Integration;
using CommunityOS.Audit.Infrastructure.Persistence;
using CommunityOS.EventBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.Audit.API.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddAuditServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuditApplication();
        services.AddAuditInfrastructure(config);
        services.AddCommunityOSEventBusWithInbox<AuditDbContext>(config, bus =>
        {
            bus.AddConsumer<RecordsAuditConsumer>();
            bus.AddConsumer<WorkflowAuditConsumer>();
            bus.AddConsumer<NotificationsAuditConsumer>();
            bus.AddConsumer<OrganizationUnitProjectionConsumer>();
            bus.AddConsumer<AuthorizationAuditConsumer>();
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
            .AddJwtBearer(x => AuditJwtValidation.Configure(x, config));
        services.AddAuthorization();
        return services;
    }
}
