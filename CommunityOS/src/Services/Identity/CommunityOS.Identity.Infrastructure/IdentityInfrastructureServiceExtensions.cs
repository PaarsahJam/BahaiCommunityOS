using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Infrastructure.ExternalServices;
using CommunityOS.Identity.Infrastructure.Persistence;
using CommunityOS.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Identity.Infrastructure;

public static class IdentityInfrastructureServiceExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<IdentityDbContext>(opts =>
            opts.UseNpgsql(
                config.GetConnectionString("IdentityDb"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(IdentityInfrastructureServiceExtensions).Assembly.FullName)));

        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        return services;
    }
}
