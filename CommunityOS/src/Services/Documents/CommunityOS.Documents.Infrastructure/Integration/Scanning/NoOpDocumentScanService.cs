using CommunityOS.Documents.Application.Abstractions;
using CommunityOS.Documents.Domain.Enumerations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CommunityOS.Documents.Infrastructure.Integration.Scanning;

namespace CommunityOS.Documents.Infrastructure.Integration.Scanning;

/// <summary>
/// Shipped no-op scanner (ADR-022, ratified): reports
/// <see cref="ScanStatus.NotScanned"/>, which never blocks download. A real
/// antivirus scanner can replace this implementation without touching the
/// application or domain layers. A document may become <c>Active</c> before
/// scanning completes; the scan state only gates content download.
/// </summary>
public sealed class NoOpDocumentScanService(
    IOptions<MalwareScanningOptions> options,
    ILogger<NoOpDocumentScanService> logger) : IDocumentScanService
{
    private readonly MalwareScanningOptions _options = options.Value;

    public Task<ScanStatus> ScanAsync(Stream content, CancellationToken ct = default)
    {
        if (_options.Enabled)
            logger.ScanningEnabledWithoutScanner();

        return Task.FromResult(ScanStatus.NotScanned);
    }
}

internal static partial class NoOpDocumentScanServiceLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Warning,
        Message = "Malware scanning is enabled but only the no-op scanner is registered; versions will be marked not_scanned and remain downloadable.")]
    public static partial void ScanningEnabledWithoutScanner(this ILogger logger);
}