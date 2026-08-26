using CommunityOS.Authorization.HttpClient.Integration;
using CommunityOS.Search.Application;
using CommunityOS.Search.Infrastructure.Integration;
using CommunityOS.Search.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace CommunityOS.Search.Infrastructure;
public static class SearchInfrastructureServiceExtensions
{
    public static IServiceCollection AddSearchInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<SearchDbContext>(o => o.UseNpgsql(config.GetConnectionString("SearchDb"), x => x.MigrationsAssembly(typeof(SearchDbContext).Assembly.FullName)));
        services.AddScoped<ISearchProjectionWriter, SearchProjectionWriter>();
        services.AddScoped<ISearchProjectionRepository, SearchProjectionRepository>();
        services.AddScoped<IProjectionReconciler, SearchProjectionReconciler>();
        services.Configure<SearchOptions>(config.GetSection(SearchOptions.SectionName));
        services.AddAuthorizationHttpClient(config, "communityos-search");
        return services;
    }
}

