namespace CommunityOS.Documents.Application.Options;

/// <summary>
/// Content-integrity configuration (ADR-022, ratified: SHA-256 verified
/// upload/download). Upload always computes the SHA-256 for content
/// addressing; <see cref="VerifyHashOnRead"/> additionally recomputes and
/// compares the hash on download so object-storage corruption is detected
/// before content is served.
/// </summary>
public sealed class DocumentsIntegrityOptions
{
    public const string SectionName = "Documents:Integrity";

    /// <summary>Recompute SHA-256 on download and compare with the stored hash.</summary>
    public bool VerifyHashOnRead { get; set; } = true;
}