namespace CommunityOS.Contracts.Localization;

/// <summary>
/// Localization integration events (ADR-029, slot 14). The ratified catalog is
/// exactly this one published fact; the payload carries codes, a version and a
/// timestamp only — never translation values or any catalog content (the
/// ratified privacy rule). It is published through the Localization
/// transactional outbox from birth (ADR-015) and has zero consumers at the
/// first gate; consumption by Audit would require the recorded future ADR-027
/// amendment.
/// </summary>
// CatalogContext: which catalog section changed ("resources" or "entities").
// Namespace: affected resource namespace, null when not applicable.
// Culture: affected culture code (BCP-47).
// BundleVersion: new global catalog version after this change.
public sealed record LocalizationCatalogChanged(
    string CatalogContext,
    string? Namespace,
    string Culture,
    long BundleVersion,
    DateTime OccurredOn);
