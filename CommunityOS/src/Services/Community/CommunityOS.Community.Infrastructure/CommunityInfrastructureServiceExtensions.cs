using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Persistence;
using CommunityOS.Community.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Community.Infrastructure;

public static class CommunityInfrastructureServiceExtensions
{
    public static IServiceCollection AddCommunityInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<CommunityDbContext>(opts =>
            opts.UseNpgsql(
                config.GetConnectionString("CommunityDb"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(CommunityInfrastructureServiceExtensions).Assembly.FullName)));

        services.AddScoped<ICommunityRepository, CommunityRepository>();

        return services;
    }
}
