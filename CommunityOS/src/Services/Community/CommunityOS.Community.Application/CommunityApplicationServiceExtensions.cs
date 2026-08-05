using CommunityOS.Community.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Community.Application;

public static class CommunityApplicationServiceExtensions
{
    public static IServiceCollection AddCommunityApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(CommunityApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<CreateCommunityCommandValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        return services;
    }
}
