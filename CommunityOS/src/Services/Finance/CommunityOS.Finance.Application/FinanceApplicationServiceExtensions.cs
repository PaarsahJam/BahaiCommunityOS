using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Finance.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Finance.Application;

public static class FinanceApplicationServiceExtensions
{
    public static IServiceCollection AddFinanceApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(FinanceApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<CreateFundCommandValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}