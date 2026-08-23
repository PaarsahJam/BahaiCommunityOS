using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CommunityOS.Audit.Domain;

/// <summary>
/// Allowlisted, flat, scalar-only metadata attached to an audit entry
/// (ADR-027 decision 6). Keys are bounded snake_case codes from a per-event
/// ingest allowlist, values are scalars rendered as invariant-culture strings,
/// and at most <see cref="MaxKeys"/> pairs are accepted. Nonconforming input
/// is rejected here so the ingest pipeline dead-letters it — metadata is never
/// silently sanitized or truncated.
/// </summary>
public sealed class AuditMetadata
{
    public const int MaxKeys = 10;
    private const int MaxKeyLength = 50;
    private const int MaxValueLength = 200;

    private AuditMetadata(IReadOnlyDictionary<string, string> values, string json)
    {
        Values = values;
        Json = json;
    }

    /// <summary>Normalized key/value pairs (empty when no metadata).</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    /// <summary>Canonical compact JSON persisted to the jsonb column; empty
    /// metadata serializes as <c>{}</c>.</summary>
    public string Json { get; }

    public static AuditMetadata Empty { get; } =
        new(new Dictionary<string, string>(StringComparer.Ordinal), "{}");

    /// <summary>Creates validated metadata from raw scalar values. Null values
    /// are rejected: omitting the key expresses absence.</summary>
    public static AuditMetadata Create(IReadOnlyDictionary<string, object?> values)
    {
        if (values.Count > MaxKeys)
        {
            throw new ArgumentException(
                $"Audit metadata allows at most {MaxKeys} keys; received {values.Count}.",
                nameof(values));
        }

        if (values.Count == 0) return Empty;

        var normalized = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, rawValue) in values)
        {
            ValidateKey(key);
            normalized[key] = NormalizeValue(key, rawValue);
        }

        return new(normalized, Serialize(normalized));
    }

    /// <summary>Rehydrates metadata previously persisted through
    /// <see cref="Json"/>. Only this class writes the column, but keys are
    /// revalidated against the allowlist anyway: the store must stay clean
    /// even if a future writer bypasses <see cref="Create"/>.</summary>
    public static AuditMetadata FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;

        using var document = JsonDocument.Parse(json);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            ValidateKey(property.Name);
            values[property.Name] = property.Value.ToString() ?? string.Empty;
        }

        return new(values, json);
    }

    /// <summary>The ratified union of per-event ingest keys (ADR-027
    /// decision 6): producer mappings may only emit these, and the domain
    /// rejects anything else so rogue payloads dead-letter.</summary>
    public static readonly IReadOnlySet<string> AllowedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        // Records
        "category", "status", "subject_type", "version_number", "supersedes_version_number",
        "classification_code", "hold_id", "hold_type", "retention_schedule_code",
        "retention_period", "reference_type",
        // Workflow
        "definition_code", "domain_type", "assignee_count", "escalated_to_count",
        // Notifications
        "type_code", "channel", "source_type", "recipient_count",
        // Audit-of-audit journal entries
        "format", "row_count", "filters", "entry_count", "reason_code",
        "purged_count", "retention_classes", "exported_count", "hold_ids_count", "released_count"
    };

    private static void ValidateKey(string key)
    {
        if (!AllowedKeys.Contains(key) || key.Length > MaxKeyLength ||
            !key.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') || key.Any(char.IsUpper))
        {
            throw new ArgumentException(
                $"Audit metadata key '{key}' is outside the ratified allowlist.", nameof(key));
        }
    }

    private static string NormalizeValue(string key, object? value) => value switch
    {
        null => throw new ArgumentException($"Audit metadata '{key}' has a null value.", nameof(value)),
        string s when string.IsNullOrWhiteSpace(s) =>
            throw new ArgumentException($"Audit metadata '{key}' has an empty value.", nameof(value)),
        string s when s.Length > MaxValueLength =>
            throw new ArgumentException($"Audit metadata '{key}' exceeds {MaxValueLength} characters.", nameof(value)),
        string s => s,
        bool b => b ? "true" : "false",
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        Guid g => g.ToString("D"),
        DateTime dt => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        _ => throw new NotSupportedException($"Audit metadata '{key}' carries a non-scalar value.")
    };

    private static string Serialize(SortedDictionary<string, string> values)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in values)
            {
                writer.WriteString(key, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
