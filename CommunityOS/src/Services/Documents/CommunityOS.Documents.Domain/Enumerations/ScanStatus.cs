using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Documents.Domain.Enumerations;

/// <summary>
/// Malware-scan lifecycle of a document version (ADR-022, ratified). The scan
/// state never gates the document lifecycle — only content download. The only
/// mutable field on an otherwise immutable version.
/// </summary>
public sealed class ScanStatus : Enumeration<int>
{
    public static readonly ScanStatus NotScanned = new(1, "not_scanned");
    public static readonly ScanStatus Quarantined = new(2, "quarantined");
    public static readonly ScanStatus Scanning = new(3, "scanning");
    public static readonly ScanStatus Clean = new(4, "clean");
    public static readonly ScanStatus Rejected = new(5, "rejected");

    private ScanStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<ScanStatus> All =>
        [NotScanned, Quarantined, Scanning, Clean, Rejected];

    /// <summary>
    /// When scanning is enabled, versions in these states are not downloadable
    /// (fail-closed). When scanning is disabled every version is
    /// <see cref="NotScanned"/> and always downloadable.
    /// </summary>
    public bool BlocksDownload =>
        Equals(Scanning) || Equals(Quarantined) || Equals(Rejected);

    public static ScanStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown ScanStatus id: {id}");

    public static ScanStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown ScanStatus name: {name}");
}