using Asp.Versioning;
using CommunityOS.EventBus;
using CommunityOS.Search.API.Security;
using CommunityOS.Search.Application;
using CommunityOS.Search.Infrastructure;
using CommunityOS.Search.Infrastructure.Integration;
using CommunityOS.Search.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CommunityOS.Search.API.Extensions;
internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddSearchServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddSearchApplication();
        services.AddSearchInfrastructure(config);
        services.AddCommunityOSEventBusWithInbox<SearchDbContext>(config, bus => { bus.AddConsumer<RecordsIndexConsumer>(); bus.AddConsumer<DocumentsIndexConsumer>(); bus.AddConsumer<WorkflowIndexConsumer>(); bus.AddConsumer<KnowledgeIndexConsumer>(); bus.AddConsumer<OrganizationUnitProjectionConsumer>(); });
        services.AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
        services.AddApiVersioning(x => { x.DefaultApiVersion = new ApiVersion(1, 0); x.AssumeDefaultVersionWhenUnspecified = true; x.ReportApiVersions = true; }).AddApiExplorer(x => { x.GroupNameFormat = "'v'VVV"; x.SubstituteApiVersionInUrl = true; });
        services.AddEndpointsApiExplorer(); services.AddSwaggerGen();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(x => SearchJwtValidation.Configure(x, config)); services.AddAuthorization();
        return services;
    }
}
