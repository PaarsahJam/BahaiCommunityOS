using CommunityOS.Documents.Domain.Enumerations;

namespace CommunityOS.Documents.Application.Abstractions;

/// <summary>
/// Malware-scanning extension point (ADR-022, ratified). The shipped
/// implementation is a no-op development scanner that reports
/// <see cref="ScanStatus.NotScanned"/>; a real scanner can replace it later
/// without touching the application or domain layers. A document may become
/// <c>Active</c> before scanning completes — the scan state only gates content
/// download, never the document lifecycle, and a scan result never silently
/// mutates unrelated document state.
/// </summary>
public interface IDocumentScanService
{
    Task<ScanStatus> ScanAsync(Stream content, CancellationToken ct = default);
}