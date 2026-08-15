namespace CommunityOS.Documents.API.Config;

/// <summary>
/// Operational configuration for the Documents API host.
/// </summary>
public sealed class DocumentsApiOptions
{
    public const string SectionName = "Documents:Upload";

    /// <summary>Maximum accepted upload size in bytes (413 beyond this).</summary>
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>MIME types accepted on version upload (415 otherwise).</summary>
    public IReadOnlyList<string> AllowedMimeTypes { get; set; } =
    [
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain",
        "text/markdown",
        "text/csv",
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    ];

    /// <summary>
    /// Identifier presented in the <c>X-Client-Id</c> header by trusted internal
    /// callers. Reserved for future service-to-service fact endpoints; no current
    /// consumer requires one.
    /// </summary>
    public string InternalClientId { get; set; } = "communityos-authorization";
}