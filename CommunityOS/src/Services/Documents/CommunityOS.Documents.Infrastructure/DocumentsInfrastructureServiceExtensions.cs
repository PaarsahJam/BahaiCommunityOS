using Amazon.Runtime;
using Amazon.S3;
using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Authorization.Application.Interfaces;
using CommunityOS.Authorization.HttpClient.Integration;
using CommunityOS.Documents.Application.Abstractions;
using CommunityOS.Documents.Application.Pipeline;
using CommunityOS.Documents.Domain.Repositories;
using CommunityOS.Documents.Infrastructure.Integration;
using CommunityOS.Documents.Infrastructure.Integration.Organization;
using CommunityOS.Documents.Infrastructure.Integration.Scanning;
using CommunityOS.Documents.Infrastructure.Integration.Storage;
using CommunityOS.Documents.Infrastructure.Persistence;
using CommunityOS.Documents.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Documents.Infrastructure;

public static class DocumentsInfrastructureServiceExtensions
{
    public static IServiceCollection AddDocumentsInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<DocumentsDbContext>(opts =>
            opts.UseNpgsql(
                config.GetConnectionString("DocumentsDb"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(DocumentsInfrastructureServiceExtensions).Assembly.FullName)));

        // Persistence
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IOrganizationUnitReferenceRepository, OrganizationUnitReferenceRepository>();

        // Object storage (ADR-022, ratified): AWSSDK.S3 client configured for
        // MinIO in development; binary content is never stored in PostgreSQL.
        services.ConfigureObjectStorage(config);
        var storage = config.GetSection(ObjectStorageOptions.SectionName).Get<ObjectStorageOptions>()
                      ?? new ObjectStorageOptions();
        var serviceUrl = storage.Endpoint.Contains("://")
            ? storage.Endpoint
            : $"{(storage.UseHttp ? "http" : "https")}://{storage.Endpoint}";
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = storage.ForcePathStyle,
                AuthenticationRegion = storage.Region
            }));
        services.AddScoped<IDocumentObjectStorage, S3ObjectStorage>();

        // Malware-scanning extension point (ADR-022, ratified): no-op scanner.
        services.ConfigureMalwareScanning(config);
        services.AddScoped<IDocumentScanService, NoOpDocumentScanService>();

        // Organization read-model (ADR-016)
        services.AddScoped<OrganizationIntegrationEventConsumer>();

        // Domain event -> integration event forwarding onto the message bus.
        // Registered as an open generic so MediatR 12.4.1 (which dispatches by
        // the runtime type of the notification) resolves the closed publisher for
        // each concrete domain event, e.g. INotificationHandler<DocumentCreatedEvent>.
        services.AddScoped(typeof(INotificationHandler<>), typeof(DocumentsIntegrationEventPublisher<>));

        // Sensitive-download audit event (ADR-022, ratified): raised only for
        // protected content actually downloaded, never for metadata reads.
        services.AddScoped<INotificationHandler<DocumentContentDownloadNotification>,
            DocumentContentDownloadNotificationHandler>();

        // Authorization integration: the Documents service never reads the
        // Authorization database. Every decision is delegated to the
        // Authorization service's check endpoint over HTTP (ADR-018/ADR-019).
        services.AddAuthorizationHttpClient(config, "communityos-documents", includeGuard: true);

        return services;
    }
}