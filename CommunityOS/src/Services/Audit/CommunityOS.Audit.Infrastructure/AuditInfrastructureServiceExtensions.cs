using CommunityOS.Audit.Application;
using CommunityOS.Audit.Infrastructure.Integration;
using CommunityOS.Audit.Infrastructure.Persistence;
using CommunityOS.Audit.Infrastructure.Retention;
using CommunityOS.Authorization.HttpClient.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Audit.Infrastructure;

public static class AuditInfrastructureServiceExtensions
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AuditDbContext>(o => o.UseNpgsql(
            config.GetConnectionString("AuditDb"),
            x =>
            {
                x.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName);
                x.MigrationsHistoryTable("__ef_migrations_history", "audit");
            }));

        services.AddScoped<IAuditIngestor, AuditIngestor>();
        services.AddScoped<IAuditReader, AuditReader>();
        services.AddScoped<IAuditJournal, AuditJournal>();
        services.AddSingleton<IRetentionPolicy, AuditRetentionPolicy>();

        services.Configure<AuditOptions>(config.GetSection(AuditOptions.SectionName));
        services.AddAuthorizationHttpClient(config, "communityos-audit");

        return services;
    }
}
