namespace CommunityOS.Workflow.Application.Permissions;

/// <summary>
/// Central registry of well-known Workflow permission names
/// (<c>workflow.entity.action</c>) — the ratified matrix (ADR-024). Enforced by
/// the Workflow application layer through the Authorization guard. No local RBAC
/// and no direct Authorization database access. Metadata and sensitive-field
/// access are separate capabilities; sensitive task fields/notes have a
/// dedicated permission.
/// </summary>
public static class WorkflowPermissions
{
    public const string TaskRead = "workflow.task.read";
    public const string TaskReadSensitive = "workflow.task.read.sensitive";
    public const string TaskCreate = "workflow.task.create";
    public const string TaskAssign = "workflow.task.assign";
    public const string TaskStart = "workflow.task.start";
    public const string TaskComplete = "workflow.task.complete";
    public const string TaskCancel = "workflow.task.cancel";
    public const string TaskEscalate = "workflow.task.escalate";
    public const string DefinitionRead = "workflow.definition.read";
    public const string DefinitionManage = "workflow.definition.manage";
    public const string TaskAdmin = "workflow.task.admin";
}