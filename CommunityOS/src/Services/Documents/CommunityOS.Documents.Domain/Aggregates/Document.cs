using CommunityOS.Documents.Domain.Enumerations;
using CommunityOS.Documents.Domain.Events;
using CommunityOS.Documents.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Documents.Domain.Aggregates;

/// <summary>
/// Aggregate root of the Documents bounded context (ADR-022): a named, ordered,
/// append-only sequence of immutable content snapshots (<see cref="DocumentVersion"/>)
/// plus the metadata governing their security, classification, organization
/// scope, retention and lifecycle. Deletion is never hard deletion during
/// normal operation; <c>Deactivated</c> preserves content and is reversible.
/// </summary>
public sealed class Document : AggregateRoot<Guid>
{
    private readonly List<DocumentVersion> _versions = [];
    private readonly List<DocumentOrganizationScope> _scopes = [];
    private readonly List<DocumentReference> _references = [];

    private Document() : base(Guid.Empty)
    {
        Title = null!;
        Status = DocumentStatus.Draft;
        Classification = DocumentClassificationMetadata.Create();
    }

    private Document(
        Guid id,
        string title,
        string? description,
        Guid? organizationUnitId,
        string? ownerType,
        Guid? ownerId,
        Guid createdBy,
        DateTime occurredOn) : base(id)
    {
        Title = title;
        Description = description;
        OrganizationUnitId = organizationUnitId;
        OwnerType = ownerType;
        OwnerId = ownerId;
        Status = DocumentStatus.Draft;
        Classification = DocumentClassificationMetadata.Create();
        CreatedBy = createdBy;
        CreatedOn = occurredOn.ToUniversalTime();
        UpdatedBy = createdBy;
        UpdatedOn = occurredOn.ToUniversalTime();
    }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public DocumentStatus Status { get; private set; }

    /// <summary>Primary organization scope. Null for person-owned documents.</summary>
    public Guid? OrganizationUnitId { get; private set; }

    /// <summary><c>person</c> | <c>organizationunit</c>.</summary>
    public string? OwnerType { get; private set; }

    public Guid? OwnerId { get; private set; }

    /// <summary>Pointer to the current <see cref="DocumentVersion"/>.</summary>
    public Guid? CurrentVersionId { get; private set; }

    public DocumentClassificationMetadata Classification { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public IReadOnlyList<DocumentVersion> Versions => _versions.AsReadOnly();

    public IReadOnlyList<DocumentOrganizationScope> Scopes => _scopes.AsReadOnly();

    public IReadOnlyList<DocumentReference> References => _references.AsReadOnly();

    public DocumentVersion? CurrentVersion =>
        _versions.FirstOrDefault(v => v.Id == CurrentVersionId);

    /// <summary>Every organization-unit scope: primary plus additional scopes.</summary>
    public IEnumerable<Guid> AllOrganizationUnitIds =>
        new[] { OrganizationUnitId }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Concat(_scopes.Select(s => s.OrganizationUnitId))
            .Distinct();

    public static Document Create(
        string title,
        string? description,
        Guid? organizationUnitId,
        string? ownerType,
        Guid? ownerId,
        Guid createdBy,
        DateTime occurredOn)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 300, nameof(title));
        if (description is not null) Guard.MaxLength(description, 2000, nameof(description));
        if (ownerType is not null) Guard.MaxLength(ownerType, 30, nameof(ownerType));
        if (ownerId is not null && ownerType is null)
            throw new ArgumentException("An owner id requires an owner type.", nameof(ownerId));

        var document = new Document(
            Guid.NewGuid(),
            title.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            organizationUnitId,
            string.IsNullOrWhiteSpace(ownerType) ? null : ownerType.Trim().ToLowerInvariant(),
            ownerId,
            createdBy,
            occurredOn);

        document.RaiseDomainEvent(new DocumentCreatedEvent(
            document.Id,
            document.Title,
            document.Status.Name,
            document.OrganizationUnitId,
            document.OwnerType,
            document.OwnerId,
            document.CreatedBy));

        return document;
    }

    /// <summary>
    /// Updates non-security metadata. Metadata changes never create a version
    /// (ADR-022). Allowed in <c>Draft</c> and <c>Active</c> only.
    /// </summary>
    public void UpdateMetadata(string title, string? description, Guid updatedBy, DateTime occurredOn)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 300, nameof(title));
        if (description is not null) Guard.MaxLength(description, 2000, nameof(description));

        EnsureCanEditMetadata();

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedBy = updatedBy;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new DocumentMetadataUpdatedEvent(Id, Status.Name, OrganizationUnitId));
    }

    /// <summary>Adds an organization scope. Scope changes are metadata changes.</summary>
    public void AddOrganizationScope(Guid organizationUnitId, Guid updatedBy, DateTime occurredOn)
    {
        Guard.NotDefault(organizationUnitId, nameof(organizationUnitId));
        EnsureCanEditMetadata();

        if (organizationUnitId == OrganizationUnitId)
            return;

        if (_scopes.Any(s => s.OrganizationUnitId == organizationUnitId))
            return;

        _scopes.Add(new DocumentOrganizationScope(Guid.NewGuid(), organizationUnitId));
        UpdatedBy = updatedBy;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new DocumentMetadataUpdatedEvent(Id, Status.Name, OrganizationUnitId));
    }

    /// <summary>Removes an organization scope.</summary>
    public void RemoveOrganizationScope(Guid organizationUnitId, Guid updatedBy, DateTime occurredOn)
    {
        Guard.NotDefault(organizationUnitId, nameof(organizationUnitId));
        EnsureCanEditMetadata();

        var scope = _scopes.FirstOrDefault(s => s.OrganizationUnitId == organizationUnitId);
        if (scope is null)
            return;

        _scopes.Remove(scope);
        UpdatedBy = updatedBy;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new DocumentMetadataUpdatedEvent(Id, Status.Name, OrganizationUnitId));
    }

    /// <summary>
    /// Sets classification/sensitive/retention/hold metadata. Allowed in any
    /// non-<c>Deactivated</c> state (holds are the only metadata edits allowed
    /// on an <c>Archived</c> document).
    /// </summary>
    public void SetClassification(
        string? classificationCode,
        bool isSensitive,
        string? retentionCategory,
        string? legalHoldReference,
        string? administrativeHoldReference,
        Guid classifiedBy,
        DateTime occurredOn)
    {
        if (Status == DocumentStatus.Deactivated)
            throw new InvalidDocumentTransitionException(Id, Status.Name, "classify");

        Classification.Classify(
            classificationCode,
            isSensitive,
            retentionCategory,
            legalHoldReference,
            administrativeHoldReference,
            classifiedBy,
            occurredOn);

        UpdatedBy = classifiedBy;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new DocumentClassifiedEvent(
            Id, Classification.ClassificationCode, Classification.IsSensitive));
    }

    /// <summary>
    /// Adds an immutable version (content is stored under the content-addressed
    /// object key; only metadata is written here). A new upload creates a new
    /// version, moves the current-version pointer, and activates a <c>Draft</c>
    /// document. Duplicate content for the same document returns the existing
    /// version (idempotent).
    /// </summary>
    public DocumentVersion AddVersion(
        string contentHash,
        string mimeType,
        long sizeBytes,
        string fileName,
        string source,
        Guid uploadedBy,
        DateTime uploadedOn,
        ScanStatus scanStatus)
    {
        Guard.NotNullOrWhiteSpace(contentHash, nameof(contentHash));
        Guard.MaxLength(contentHash, 64, nameof(contentHash));
        Guard.NotNullOrWhiteSpace(mimeType, nameof(mimeType));
        Guard.MaxLength(mimeType, 200, nameof(mimeType));
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Value must be > 0.");
        Guard.NotNullOrWhiteSpace(fileName, nameof(fileName));
        Guard.MaxLength(fileName, 255, nameof(fileName));
        Guard.NotNullOrWhiteSpace(source, nameof(source));

        if (Status == DocumentStatus.Archived || Status == DocumentStatus.Deactivated)
            throw new InvalidDocumentTransitionException(Id, Status.Name, "add version");

        var normalizedHash = contentHash.Trim().ToLowerInvariant();

        var existing = _versions.FirstOrDefault(v => v.ContentHash == normalizedHash);
        if (existing is not null)
            return existing;

        var version = new DocumentVersion(
            Guid.NewGuid(),
            _versions.Count + 1,
            normalizedHash,
            $"documents/{normalizedHash}",
            mimeType.Trim().ToLowerInvariant(),
            sizeBytes,
            fileName.Trim(),
            source.Trim().ToLowerInvariant(),
            uploadedBy,
            uploadedOn.ToUniversalTime(),
            scanStatus);

        _versions.Add(version);
        CurrentVersionId = version.Id;
        UpdatedBy = uploadedBy;
        UpdatedOn = uploadedOn.ToUniversalTime();

        if (Status == DocumentStatus.Draft)
            Status = DocumentStatus.Active;

        RaiseDomainEvent(new DocumentVersionAddedEvent(
            Id, version.Id, version.VersionNumber, version.MimeType, version.SizeBytes,
            version.ContentHash, version.UploadedBy));

        return version;
    }

    /// <summary>Transitions <c>Active → Archived</c> (read-only retention).</summary>
    public void Archive(Guid actorId, DateTime occurredOn)
    {
        if (Status != DocumentStatus.Active)
            throw new InvalidDocumentTransitionException(Id, Status.Name, DocumentStatus.Archived.Name);

        Status = DocumentStatus.Archived;
        UpdatedBy = actorId;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new DocumentArchivedEvent(Id));
    }

    /// <summary>
    /// Transitions <c>Active/Archived → Deactivated</c> (reversible soft-delete).
    /// A legal/administrative hold blocks deactivation unless an explicitly
    /// authorized administrative override is used (ADR-022, ratified).
    /// </summary>
    public void Deactivate(Guid actorId, bool adminOverride, string? reason, DateTime occurredOn)
    {
        if (Status != DocumentStatus.Active && Status != DocumentStatus.Archived)
            throw new InvalidDocumentTransitionException(Id, Status.Name, DocumentStatus.Deactivated.Name);

        if (Classification.HasHoldReference && !adminOverride)
            throw new HeldDocumentDeactivationException(Id);

        Status = DocumentStatus.Deactivated;
        UpdatedBy = actorId;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new DocumentDeactivatedEvent(Id));
    }

    /// <summary>Restores <c>Archived</c>/<c>Deactivated</c> → <c>Active</c>.</summary>
    public void Restore(Guid actorId, DateTime occurredOn)
    {
        if (Status != DocumentStatus.Archived && Status != DocumentStatus.Deactivated)
            throw new InvalidDocumentTransitionException(Id, Status.Name, DocumentStatus.Active.Name);

        Status = DocumentStatus.Active;
        UpdatedBy = actorId;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new DocumentRestoredEvent(Id, Status.Name));
    }

    /// <summary>
    /// Records a scan result on a version. A scan result must not silently
    /// mutate unrelated document state — only the version's scan status
    /// changes, and the scan completion is raised.
    /// </summary>
    public void UpdateScanStatus(Guid versionId, ScanStatus scanStatus, DateTime occurredOn)
    {
        var version = _versions.FirstOrDefault(v => v.Id == versionId)
            ?? throw new DocumentVersionNotFoundException(Id, -1);

        version.SetScanStatus(scanStatus);

        RaiseDomainEvent(new DocumentScanCompletedEvent(Id, versionId, scanStatus.Name));
    }

    /// <summary>Adds an attachment/evidence reference. Idempotent.</summary>
    public DocumentReference AddReference(
        string sourceContext,
        Guid sourceEntityId,
        string referenceType,
        Guid createdBy,
        DateTime createdOn)
    {
        Guard.NotNullOrWhiteSpace(sourceContext, nameof(sourceContext));
        Guard.MaxLength(sourceContext, 100, nameof(sourceContext));
        Guard.NotNullOrWhiteSpace(referenceType, nameof(referenceType));
        Guard.MaxLength(referenceType, 50, nameof(referenceType));
        Guard.NotDefault(sourceEntityId, nameof(sourceEntityId));

        var existing = _references.FirstOrDefault(r =>
            string.Equals(r.SourceContext, sourceContext.Trim(), StringComparison.OrdinalIgnoreCase) &&
            r.SourceEntityId == sourceEntityId &&
            string.Equals(r.ReferenceType, referenceType.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var reference = new DocumentReference(
            Guid.NewGuid(),
            sourceContext.Trim().ToLowerInvariant(),
            sourceEntityId,
            referenceType.Trim().ToLowerInvariant(),
            createdBy,
            createdOn.ToUniversalTime());

        _references.Add(reference);
        return reference;
    }

    private void EnsureCanEditMetadata()
    {
        if (Status == DocumentStatus.Archived)
            throw new InvalidDocumentTransitionException(Id, Status.Name, "metadata update");
        if (Status == DocumentStatus.Deactivated)
            throw new InvalidDocumentTransitionException(Id, Status.Name, "metadata update");
    }
}