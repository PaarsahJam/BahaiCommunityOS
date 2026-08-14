using CommunityOS.Identity.Application.Interfaces;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Infrastructure.Integration;
using CommunityOS.Identity.Infrastructure.Persistence;
using CommunityOS.Identity.Infrastructure.Repositories;
using CommunityOS.Identity.Infrastructure.Security;
using MediatR;
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

        // Repositories
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IRecoveryRequestRepository, RecoveryRequestRepository>();
        services.AddScoped<ISecurityEventRepository, SecurityEventRepository>();
        services.AddScoped<IOAuthClientRepository, OAuthClientRepository>();
        services.AddScoped<IAuthorizationCodeRepository, AuthorizationCodeRepository>();

        // Security services
        services.AddSingleton<RsaSigningKeyProvider>();
        services.AddSingleton<ISigningKeyProvider>(sp => sp.GetRequiredService<RsaSigningKeyProvider>());
        services.AddSingleton<OidcDiscoveryDocument>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher>(sp => new Pbkdf2PasswordHasher(config.GetSection("PasswordHashing")));
        services.AddSingleton<ITotpService, TotpService>();

        // Domain event -> integration event forwarding onto the message bus.
        // Registered as an open generic so MediatR 12.4.1 (which dispatches by
        // the runtime type of the notification) resolves the closed publisher for
        // each concrete domain event.
        services.AddScoped(typeof(INotificationHandler<>), typeof(IdentityIntegrationEventPublisher<>));

        return services;
    }
}
