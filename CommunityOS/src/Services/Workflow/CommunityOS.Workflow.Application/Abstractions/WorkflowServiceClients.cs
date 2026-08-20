namespace CommunityOS.Workflow.Application.Abstractions;

/// <summary>
/// Outbound integration surface to the Documents service (ADR-024). Workflow
/// references documents only through the existing <c>DocumentReference</c>
/// mechanism with <c>SourceContext = "workflow.task"</c>; Documents remains the
/// owner of artifact bytes and metadata. Workflow needs no additional Documents
/// operations beyond creating a task reference and reading metadata, and stores
/// no document content. An unconfigured <c>DocumentsService:BaseUrl</c> is a
/// configuration error that fails closed with a clear message (runbook).
/// </summary>
public interface IDocumentsServiceClient
{
    /// <summary>
    /// Creates a Documents-side reference (<c>SourceContext = "workflow.task"</c>)
    /// mirroring a task's document reference.
    /// </summary>
    Task CreateTaskReferenceAsync(
        Guid documentId,
        Guid taskId,
        string referenceType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outbound integration surface to the Community service (ADR-024). Workflow
/// resolves assignee/person details through the Community API at read time and
/// stores only stable person ids — never names or PII. An unconfigured
/// <c>CommunityService:BaseUrl</c> is a configuration error that fails closed
/// with a clear message (runbook).
/// </summary>
public interface ICommunityServiceClient
{
    /// <summary>
    /// Returns whether the stable person id exists in the Community service.
    /// Names are never stored in Workflow.
    /// </summary>
    Task<bool> PersonExistsAsync(Guid personId, CancellationToken cancellationToken = default);
}