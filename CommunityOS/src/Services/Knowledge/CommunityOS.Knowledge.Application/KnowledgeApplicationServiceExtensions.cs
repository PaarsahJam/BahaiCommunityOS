using CommunityOS.Knowledge.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Knowledge.Application;

public static class KnowledgeApplicationServiceExtensions
{
    public static IServiceCollection AddKnowledgeApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(KnowledgeApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<CreateWorkCommandValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        return services;
    }
}