namespace CommunityOS.Records.Application.Abstractions;

/// <summary>
/// The classification metadata of a document as understood by the Documents
/// service. Used for the full-replacement classify operation (ADR-023): when
/// Records places or releases a hold it must read the document's current
/// classification/sensitive/retention values and resubmit them alongside the
/// changed hold reference, or those values would be overwritten.
/// </summary>
public sealed record DocumentClassificationState(
    string? ClassificationCode,
    bool IsSensitive,
    string? RetentionCategory,
    string? LegalHoldReference,
    string? AdministrativeHoldReference);

/// <summary>
/// Outbound integration surface to the Documents service. Records never reads
/// the Documents database; evidence references and document-level hold
/// references are commanded through the Documents API as the
/// <c>communityos-records</c> service principal. Failures propagate so a
/// Records write never commits with an inconsistent Documents side.
/// </summary>
public interface IDocumentsServiceClient
{
    /// <summary>Gets the current classification metadata of a document.</summary>
    Task<DocumentClassificationState> GetClassificationAsync(
        Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Full-replacement classify: writes the supplied classification metadata
    /// (including hold references) onto the document.
    /// </summary>
    Task ClassifyAsync(
        Guid documentId,
        DocumentClassificationState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Documents-side reference (<c>SourceContext = records.record</c>)
    /// mirroring an attached evidence reference.
    /// </summary>
    Task CreateReferenceAsync(
        Guid documentId,
        string sourceContext,
        Guid sourceEntityId,
        string referenceType,
        CancellationToken cancellationToken = default);
}