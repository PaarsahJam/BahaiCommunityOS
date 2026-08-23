using CommunityOS.Audit.Application;
using CommunityOS.Audit.Infrastructure.Integration;
using CommunityOS.Audit.Infrastructure.Persistence;
using CommunityOS.Audit.Infrastructure.Retention;
using CommunityOS.Authorization.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.Configure<AuditAuthorizationOptions>(config.GetSection("AuthorizationService"));
        services.AddHttpClient<HttpAuthorizationEvaluator>((sp, client) =>
            client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<AuditAuthorizationOptions>>().Value.BaseUrl));
        services.AddScoped<IAuthorizationEvaluator>(sp => sp.GetRequiredService<HttpAuthorizationEvaluator>());

        return services;
    }
}
