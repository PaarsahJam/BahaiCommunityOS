using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Documents.Infrastructure.Integration.Storage;

/// <summary>
/// Configuration for S3-compatible object storage behind the application's
/// storage abstraction (ADR-022, ratified). The initial deployment uses MinIO;
/// the AWSSDK.S3 client speaks the S3 protocol so the storage tier can be
/// swapped to AWS S3 by changing only this configuration.
/// </summary>
public sealed class ObjectStorageOptions
{
    public const string SectionName = "Documents:Storage";

    /// <summary>Bucket holding document content. Bootstrapped by docker init.</summary>
    public string Bucket { get; set; } = "communityos-documents";

    /// <summary>Service URL of the S3-compatible endpoint (e.g. http://localhost:9000).</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Authentication region. S3-compatible stores accept us-east-1.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Path-style addressing is required by MinIO; AWS uses virtual-hosted.</summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>Secure transport in production; plain HTTP for local MinIO.</summary>
    public bool UseHttp { get; set; } = true;

    /// <summary>
    /// Records and enforces storage-layer encryption at rest (ADR-022,
    /// ratified). When true, written objects request AES256 server-side
    /// encryption.
    /// </summary>
    public bool EncryptionAtRest { get; set; } = true;
}

public static class ObjectStorageOptionsExtensions
{
    public static IServiceCollection ConfigureObjectStorage(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<ObjectStorageOptions>()
            .Bind(config.GetSection(ObjectStorageOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Bucket), "Documents:Storage:Bucket is required.");
        return services;
    }
}