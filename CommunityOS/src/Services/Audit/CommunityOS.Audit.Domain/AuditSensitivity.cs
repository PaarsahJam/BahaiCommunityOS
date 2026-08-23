namespace CommunityOS.Audit.Domain;

/// <summary>
/// Deterministic sensitivity classification of an audit entry (ADR-027
/// decision 6). An entry is <see cref="Sensitive"/> when its event type is on
/// the ratified sensitive-event list or when the producer payload explicitly
/// carries <c>IsSensitive = true</c>; every other entry is
/// <see cref="Normal"/>. Sensitivity governs visibility (a separate
/// second-pass permission), never field masking.
/// </summary>
public enum AuditSensitivity
{
    Normal = 0,
    Sensitive = 1
}
