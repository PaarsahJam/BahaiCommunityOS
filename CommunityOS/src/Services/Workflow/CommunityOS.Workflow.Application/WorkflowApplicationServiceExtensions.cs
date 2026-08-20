using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Workflow.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Workflow.Application;

public static class WorkflowApplicationServiceExtensions
{
    public static IServiceCollection AddWorkflowApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(WorkflowApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<CreateWorkflowTaskCommandValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}