using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.AI.Application.Commands;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.AI.Application;

public static class AiApplicationServiceExtensions
{
    public static IServiceCollection AddAiApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(AiApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<AssistInvocationValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}
