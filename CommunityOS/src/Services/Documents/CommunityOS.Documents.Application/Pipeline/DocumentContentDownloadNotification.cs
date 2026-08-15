using MediatR;

namespace CommunityOS.Documents.Application.Pipeline;

/// <summary>
/// Raised when protected (sensitive) document content is actually downloaded.
/// A MediatR notification (not a domain event — the aggregate is untouched) so
/// the Infrastructure layer can forward it as the ratified
/// <c>DocumentContentDownloaded</c> integration event without leaking content.
/// Ordinary metadata reads are never notified.
/// </summary>
public sealed record DocumentContentDownloadNotification(
    Guid DocumentId,
    Guid VersionId,
    Guid ActorId) : INotification;