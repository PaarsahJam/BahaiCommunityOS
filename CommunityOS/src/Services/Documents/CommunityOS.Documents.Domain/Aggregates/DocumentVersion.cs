using CommunityOS.Documents.Domain.Enumerations;
using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Documents.Domain.Aggregates;

/// <summary>
/// An immutable content snapshot of a document (ADR-022). Every field is
/// immutable once written except <see cref="ScanStatus"/>, which is the only
/// mutation allowed on an otherwise immutable version. A new upload creates a
/// new version and moves the document's current-version pointer; existing
/// versions are never mutated.
/// </summary>
public sealed class DocumentVersion : Entity<Guid>
{
    private DocumentVersion() : base(Guid.Empty)
    {
        ContentHash = null!;
        ObjectKey = null!;
        MimeType = null!;
        FileName = null!;
        Source = null!;
        ScanStatus = ScanStatus.NotScanned;
    }

    internal DocumentVersion(
        Guid id,
        int versionNumber,
        string contentHash,
        string objectKey,
        string mimeType,
        long sizeBytes,
        string fileName,
        string source,
        Guid uploadedBy,
        DateTime uploadedOn,
        ScanStatus scanStatus) : base(id)
    {
        VersionNumber = versionNumber;
        ContentHash = contentHash;
        ObjectKey = objectKey;
        MimeType = mimeType;
        SizeBytes = sizeBytes;
        FileName = fileName;
        Source = source;
        UploadedBy = uploadedBy;
        UploadedOn = uploadedOn;
        ScanStatus = scanStatus;
    }

    /// <summary>1-based, monotonically increasing per document, assigned by the service.</summary>
    public int VersionNumber { get; private set; }

    /// <summary>SHA-256 of the bytes, lowercase hex.</summary>
    public string ContentHash { get; private set; }

    /// <summary>Content-addressed storage key <c>documents/{sha256}</c>.</summary>
    public string ObjectKey { get; private set; }

    public string MimeType { get; private set; }

    public long SizeBytes { get; private set; }

    public string FileName { get; private set; }

    /// <summary><c>member</c> | <c>system</c> | <c>import</c>.</summary>
    public string Source { get; private set; }

    public Guid UploadedBy { get; private set; }

    public DateTime UploadedOn { get; private set; }

    /// <summary>The only mutable field. Never gates the document lifecycle — only download.</summary>
    public ScanStatus ScanStatus { get; private set; }

    internal void SetScanStatus(ScanStatus scanStatus) => ScanStatus = scanStatus;
}