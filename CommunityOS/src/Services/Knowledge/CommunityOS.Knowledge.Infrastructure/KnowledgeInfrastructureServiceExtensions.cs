using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.HttpClient.Integration;
using CommunityOS.Knowledge.Domain.Repositories;
using CommunityOS.Knowledge.Infrastructure.Integration;
using CommunityOS.Knowledge.Infrastructure.Integration.Organization;
using CommunityOS.Knowledge.Infrastructure.Persistence;
using CommunityOS.Knowledge.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Knowledge.Infrastructure;

public static class KnowledgeInfrastructureServiceExtensions
{
    public static IServiceCollection AddKnowledgeInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<KnowledgeDbContext>(opts =>
            opts.UseNpgsql(
                config.GetConnectionString("KnowledgeDb"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(KnowledgeInfrastructureServiceExtensions).Assembly.FullName)));

        // Library
        services.AddScoped<IWorkRepository, WorkRepository>();
        services.AddScoped<IEditionRepository, EditionRepository>();
        services.AddScoped<IPassageRepository, PassageRepository>();

        // Community knowledge
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IAnswerRepository, AnswerRepository>();
        services.AddScoped<IDiscussionRepository, DiscussionRepository>();
        services.AddScoped<IReferenceRepository, ReferenceRepository>();
        services.AddScoped<IAiSuggestionRepository, AiSuggestionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITopicRepository, TopicRepository>();

        // Organization read-model (ADR-016)
        services.AddScoped<IOrganizationUnitReferenceRepository, OrganizationUnitReferenceRepository>();
        services.AddScoped<OrganizationIntegrationEventConsumer>();

        // Domain event -> integration event forwarding onto the message bus.
        // Registered as an open generic so MediatR 12.4.1 (which dispatches by
        // the runtime type of the notification) resolves the closed publisher for
        // each concrete domain event, e.g. INotificationHandler<AnswerAddedEvent>.
        services.AddScoped(typeof(INotificationHandler<>), typeof(KnowledgeIntegrationEventPublisher<>));

        // Authorization integration: the Knowledge service never reads the
        // Authorization database. Every decision is delegated to the
        // Authorization service's check endpoint over HTTP (ADR-018/ADR-019).
        services.AddAuthorizationHttpClient(config, "communityos-knowledge", includeGuard: true);

        return services;
    }
}