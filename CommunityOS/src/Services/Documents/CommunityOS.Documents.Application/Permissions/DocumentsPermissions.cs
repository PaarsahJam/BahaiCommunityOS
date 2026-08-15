namespace CommunityOS.Documents.Application.Permissions;

/// <summary>
/// Central registry of well-known Documents permission names
/// (<c>documents.entity.action</c>) — the ratified matrix (ADR-022). Enforced
/// by the Documents application layer through the Authorization guard.
/// Metadata and content are separate capabilities; content has a sensitive
/// tier. No local RBAC and no direct Authorization database access.
/// </summary>
public static class DocumentsPermissions
{
    public const string DocumentCreate = "documents.document.create";
    public const string DocumentRead = "documents.document.read";
    public const string DocumentMetadataUpdate = "documents.document.metadata.update";
    public const string DocumentScopeManage = "documents.document.scope.manage";
    public const string DocumentContentRead = "documents.document.content.read";
    public const string DocumentContentReadSensitive = "documents.document.content.read.sensitive";
    public const string DocumentVersionCreate = "documents.document.version.create";
    public const string DocumentClassify = "documents.document.classify";
    public const string DocumentArchive = "documents.document.archive";
    public const string DocumentDeactivate = "documents.document.deactivate";
    public const string DocumentRestore = "documents.document.restore";
    public const string DocumentReference = "documents.document.reference";
    public const string DocumentReferenceRead = "documents.document.reference.read";
    public const string DocumentAdmin = "documents.document.admin";
}