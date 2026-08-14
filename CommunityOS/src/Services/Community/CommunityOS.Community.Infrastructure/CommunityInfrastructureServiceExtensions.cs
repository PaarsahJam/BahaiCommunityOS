using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Community.Domain.Repositories;
using CommunityOS.Community.Infrastructure.Integration;
using CommunityOS.Community.Infrastructure.Integration.Authorization;
using CommunityOS.Community.Infrastructure.Integration.Organization;
using CommunityOS.Community.Infrastructure.Persistence;
using CommunityOS.Community.Infrastructure.Repositories;
using MediatR;
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

        // Hierarchy read-model (deprecated Community-owned hierarchy, ADR-016)
        services.AddScoped<ICommunityRepository, CommunityRepository>();
        services.AddScoped<IOrganizationReferenceRepository, OrganizationReferenceRepository>();
        services.AddScoped<OrganizationIntegrationEventConsumer>();

        // Community life aggregates
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IHouseholdRepository, HouseholdRepository>();
        services.AddScoped<IFamilyRelationshipRepository, FamilyRelationshipRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<ICommunityEventRepository, CommunityEventRepository>();
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddScoped<IParticipationRepository, ParticipationRepository>();

        // Domain event -> integration event forwarding onto the message bus.
        // Registered as an open generic so MediatR 12.4.1 (which dispatches by
        // the runtime type of the notification) resolves the closed publisher for
        // each concrete domain event.
        services.AddScoped(typeof(INotificationHandler<>), typeof(CommunityIntegrationEventPublisher<>));

        // Authorization integration: the Community service never reads the
        // Authorization database. Every decision is delegated to the
        // Authorization service's check endpoint over HTTP (ADR-018/ADR-019).
        services.ConfigureAuthorizationService(config);
        services.AddHttpClient<HttpAuthorizationEvaluator>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationServiceOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
        });
        services.AddScoped<IAuthorizationEvaluator>(sp => sp.GetRequiredService<HttpAuthorizationEvaluator>());
        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}
