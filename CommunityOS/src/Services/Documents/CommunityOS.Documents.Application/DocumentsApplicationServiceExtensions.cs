using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Documents.Application.Permissions;
using CommunityOS.Documents.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Documents.Application;

public static class DocumentsApplicationServiceExtensions
{
    public static IServiceCollection AddDocumentsApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DocumentsApplicationServiceExtensions).Assembly));

        services.AddValidatorsFromAssemblyContaining<CreateDocumentCommandValidator>();

        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>));

        services.AddScoped<AuthorizationGuard>();

        return services;
    }
}