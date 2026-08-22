using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
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
        services.Configure<SearchAuthorizationOptions>(config.GetSection("AuthorizationService"));
        services.AddHttpClient<HttpAuthorizationEvaluator>((sp, client) => client.BaseAddress = new Uri(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SearchAuthorizationOptions>>().Value.BaseUrl));
        services.AddScoped<IAuthorizationEvaluator>(sp => sp.GetRequiredService<HttpAuthorizationEvaluator>());
        return services;
    }
}
public sealed class SearchAuthorizationOptions { public string BaseUrl { get; set; } = ""; public string AccessToken { get; set; } = ""; public string ClientId { get; set; } = "communityos-search"; }
