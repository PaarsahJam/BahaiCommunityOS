using CommunityOS.Authorization.Application.Permissions;

namespace CommunityOS.Audit.Application.Permissions;

/// <summary>
/// Audit capabilities (audit.*, ratified ADR-027 decision 12). Reading
/// sensitive entries is a separate capability from ordinary reads, and the
/// administrative capability never implies read access — hold placement still
/// requires read-level visibility of its targets.
/// </summary>
public static class AuditPermissions
{
    /// <summary>Resource type used in authorization contexts for journal entries.</summary>
    public const string ResourceType = "audit.entry";

    public const string EntryRead = "audit.entry.read";
    public const string EntryReadSensitive = "audit.entry.read.sensitive";
    public const string EntryExport = "audit.entry.export";
    public const string EntryAdmin = "audit.entry.admin";
}
