using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Search.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Search.Application;

public static class SearchApplicationServiceExtensions
{
    public static IServiceCollection AddSearchApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(SearchApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<SearchQueryValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}
