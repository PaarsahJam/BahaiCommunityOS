namespace CommunityOS.Records.Application.Permissions;

/// <summary>
/// Central registry of well-known Records permission names
/// (<c>records.entity.action</c>) — the ratified matrix (ADR-023). Enforced by
/// the Records application layer through the Authorization guard. No local RBAC
/// and no direct Authorization database access. Metadata and sensitive-field
/// access are separate capabilities; sensitive fields have a dedicated
/// permission.
/// </summary>
public static class RecordsPermissions
{
    public const string RecordCreate = "records.record.create";
    public const string RecordRead = "records.record.read";
    public const string RecordReadSensitive = "records.record.read.sensitive";
    public const string RecordUpdate = "records.record.update";
    public const string RecordSubmit = "records.record.submit";
    public const string RecordVerify = "records.record.verify";
    public const string RecordCorrect = "records.record.correct";
    public const string RecordArchive = "records.record.archive";
    public const string RecordDeactivate = "records.record.deactivate";
    public const string RecordRestore = "records.record.restore";
    public const string RecordClassify = "records.record.classify";
    public const string RecordScopeManage = "records.record.scope.manage";
    public const string RecordEvidenceManage = "records.record.evidence.manage";
    public const string RetentionManage = "records.retention.manage";
    public const string HoldManage = "records.hold.manage";
    public const string CategoryManage = "records.category.manage";
    public const string RecordAdmin = "records.record.admin";
}