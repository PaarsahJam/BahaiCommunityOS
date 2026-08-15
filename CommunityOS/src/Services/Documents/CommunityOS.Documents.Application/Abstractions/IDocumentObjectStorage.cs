namespace CommunityOS.Documents.Application.Abstractions;

/// <summary>
/// Abstraction over binary content storage (ADR-022, ratified). Binary content
/// is never stored in PostgreSQL; it lives in S3-compatible object storage
/// behind this abstraction. Object keys are content-addressed
/// (<c>documents/{sha256}</c>). Domain and application layers depend only on
/// this interface — never on AWSSDK.S3 or any MinIO-specific API.
/// </summary>
public interface IDocumentObjectStorage
{
    /// <summary>Streams content to object storage under the given key.</summary>
    Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Returns a stream to the object, or <c>null</c> if it does not exist.</summary>
    Task<Stream?> GetAsync(string objectKey, CancellationToken ct = default);

    /// <summary>Deletes the object. Used to clean up orphaned uploads.</summary>
    Task DeleteAsync(string objectKey, CancellationToken ct = default);

    /// <summary>Returns whether an object exists under the given key.</summary>
    Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default);
}