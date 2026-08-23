using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CommunityOS.Audit.Domain;

/// <summary>
/// Content-addressed identity of an audit entry (ADR-027 decision 10): the
/// lowercase SHA-256 hex digest of a canonical projection of the producing
/// event. The unique constraint on <see cref="AuditEntry.SourceEventHash"/>
/// makes ingestion idempotent — broker redelivery of the same message and
/// semantic republication of the same fact (same occurred-on) converge to one
/// journal identity, while a restatement with a different timestamp is a new,
/// distinct occurrence.
/// </summary>
public static class SourceEventHash
{
    public const int Length = 64;

    public static string Compute(
        string sourceService,
        string sourceEventType,
        string resourceType,
        Guid resourceId,
        Guid? secondaryResourceId,
        DateTime occurredOn,
        string discriminator)
    {
        var canonical = string.Create(CultureInfo.InvariantCulture, $"{sourceService}\n{sourceEventType}\n{resourceType}\n{resourceId:D}\n{secondaryResourceId?.ToString("D") ?? "-"}\n{Normalize(occurredOn).ToString("O", CultureInfo.InvariantCulture)}\n{discriminator}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>True when the value has the canonical 64-char lowercase hex
    /// shape; enforced at entry creation so the uniqueness contract stays
    /// meaningful.</summary>
    public static bool IsValidShape(string value)
    {
        if (value.Length != Length) return false;
        foreach (var c in value)
        {
            if (!(c is >= '0' and <= '9' || c is >= 'a' and <= 'f')) return false;
        }

        return true;
    }

    private static DateTime Normalize(DateTime occurredOn) =>
        occurredOn.Kind switch
        {
            DateTimeKind.Utc => occurredOn,
            DateTimeKind.Local => occurredOn.ToUniversalTime(),
            _ => DateTime.SpecifyKind(occurredOn, DateTimeKind.Utc)
        };
}
