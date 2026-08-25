using CommunityOS.AI.Domain;
using CommunityOS.AI.Application;
using CommunityOS.AI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.AI.Infrastructure;

public static class AiInfrastructureServiceExtensions
{
    public static IServiceCollection AddAiInfrastructure(this IServiceCollection services)
    {
        // Provider seam: disabled at this gate (ADR-030 decision 6, OQ-3 unresolved).
        services.AddSingleton<IAiModelGateway, DisabledAiModelGateway>();

        // Safety enforcer (ADR-030 decision 3, 5).
        services.AddSingleton<AiSafetyEnforcer>();

        return services;
    }
}
