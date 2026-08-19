using CommunityOS.Records.Domain.Enumerations;
using CommunityOS.Records.Domain.Events;
using CommunityOS.Records.Domain.Exceptions;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Records.Domain.Aggregates;

/// <summary>
/// Aggregate root of the Records bounded context (ADR-023): an official,
/// categorized, versioned record of a community or administrative fact, with an
/// immutable authoritative version once verified, references to document
/// evidence, retention and hold governance, and a guarded lifecycle
/// <c>Draft → Submitted → Under Review → Verified → Archived | Deactivated</c>
/// plus <c>Rejected</c>. Deletion is never hard deletion during normal
/// operation; <c>Deactivated</c> preserves data and is reversible.
/// </summary>
public sealed class Record : AggregateRoot<Guid>
{
    private readonly List<RecordVersion> _versions = [];
    private readonly List<RecordOrganizationScope> _scopes = [];
    private readonly List<RecordEvidenceReference> _evidence = [];
    private readonly List<RecordHold> _holds = [];
    private readonly List<RecordFieldValue> _workingFields = [];

    private Record() : base(Guid.Empty)
    {
        Category = null!;
        SubjectType = null!;
        Status = RecordStatus.Draft;
        Classification = RecordClassificationMetadata.Create();
    }

    private Record(
        Guid id,
        string category,
        string subjectType,
        Guid subjectId,
        Guid? organizationUnitId,
        IReadOnlyList<RecordFieldValue> workingFields,
        bool isSensitive,
        Guid createdBy,
        DateTime occurredOn) : base(id)
    {
        Category = category;
        SubjectType = subjectType;
        SubjectId = subjectId;
        OrganizationUnitId = organizationUnitId;
        Status = RecordStatus.Draft;
        _workingFields.AddRange(workingFields);
        Classification = RecordClassificationMetadata.Create();
        if (isSensitive)
            Classification.Classify(null, true, null, createdBy, occurredOn);
        CreatedBy = createdBy;
        CreatedOn = occurredOn.ToUniversalTime();
        UpdatedBy = createdBy;
        UpdatedOn = occurredOn.ToUniversalTime();
    }

    public string Category { get; private set; }

    public RecordStatus Status { get; private set; }

    /// <summary><c>person</c> | <c>household</c> | <c>organizationunit</c> | <c>other</c>.</summary>
    public string SubjectType { get; private set; }

    /// <summary>Stable person/household/unit id, resolved through the owning service. Never a name or PII.</summary>
    public Guid SubjectId { get; private set; }

    /// <summary>Primary organization scope. Nullable for records without a unit scope.</summary>
    public Guid? OrganizationUnitId { get; private set; }

    /// <summary>Pointer to the current authoritative <see cref="RecordVersion"/>.</summary>
    public Guid? CurrentVersionId { get; private set; }

    public RecordClassificationMetadata Classification { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public Guid? VerifiedBy { get; private set; }

    public DateTime? VerifiedOn { get; private set; }

    public IReadOnlyList<RecordVersion> Versions => _versions.AsReadOnly();

    public IReadOnlyList<RecordOrganizationScope> Scopes => _scopes.AsReadOnly();

    public IReadOnlyList<RecordEvidenceReference> Evidence => _evidence.AsReadOnly();

    public IReadOnlyList<RecordHold> Holds => _holds.AsReadOnly();

    /// <summary>Editable field set while the record is not yet verified.</summary>
    public IReadOnlyList<RecordFieldValue> WorkingFields => _workingFields.AsReadOnly();

    public RecordVersion? CurrentVersion =>
        _versions.FirstOrDefault(v => v.Id == CurrentVersionId);

    public bool IsVerified => Status == RecordStatus.Verified;

    public bool HasActiveHold => _holds.Any(h => h.IsActive);

    /// <summary>Every organization-unit scope: primary plus additional scopes.</summary>
    public IEnumerable<Guid> AllOrganizationUnitIds =>
        new[] { OrganizationUnitId }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Concat(_scopes.Select(s => s.OrganizationUnitId))
            .Distinct();

    /// <summary>Creates a Draft record with the given working field set.</summary>
    public static Record Create(
        string category,
        string subjectType,
        Guid subjectId,
        Guid? organizationUnitId,
        IReadOnlyList<RecordFieldValue> workingFields,
        bool isSensitive,
        Guid createdBy,
        DateTime occurredOn)
    {
        Guard.NotNullOrWhiteSpace(category, nameof(category));
        Guard.MaxLength(category, 100, nameof(category));
        Guard.NotNullOrWhiteSpace(subjectType, nameof(subjectType));
        Guard.MaxLength(subjectType, 30, nameof(subjectType));
        Guard.NotDefault(subjectId, nameof(subjectId));
        ValidateFields(workingFields);

        var record = new Record(
            Guid.NewGuid(),
            category.Trim(),
            subjectType.Trim().ToLowerInvariant(),
            subjectId,
            organizationUnitId,
            workingFields,
            isSensitive,
            createdBy,
            occurredOn);

        record.RaiseDomainEvent(new RecordCreatedEvent(
            record.Id,
            record.Category,
            record.Status.Name,
            record.SubjectType,
            record.SubjectId,
            record.OrganizationUnitId,
            record.CreatedBy));

        return record;
    }

    /// <summary>
    /// Replaces the working field set. Non-authoritative fields remain editable
    /// in <c>Draft</c>, <c>Submitted</c> and <c>Under Review</c>
    /// (<c>records.record.update</c>). Once <c>Verified</c>, field changes require
    /// the <c>correct</c> operation — never an in-place update.
    /// </summary>
    public void UpdateFields(IReadOnlyList<RecordFieldValue> fields, Guid updatedBy, DateTime occurredOn)
    {
        ValidateFields(fields);

        if (Status == RecordStatus.Verified ||
            Status == RecordStatus.Archived ||
            Status == RecordStatus.Deactivated)
            throw new VerifiedRecordFieldUpdateException(Id);

        _workingFields.Clear();
        _workingFields.AddRange(fields);
        UpdatedBy = updatedBy;
        UpdatedOn = occurredOn.ToUniversalTime();
    }

    /// <summary>Transitions <c>Draft → Submitted</c>.</summary>
    public void Submit(Guid actorId, DateTime occurredOn)
    {
        if (Status != RecordStatus.Draft)
            throw new InvalidRecordTransitionException(Id, Status.Name, RecordStatus.Submitted.Name);

        Status = RecordStatus.Submitted;
        UpdatedBy = actorId;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new RecordSubmittedEvent(Id, Status.Name));
    }

    /// <summary>Transitions <c>Submitted → Under Review</c>. The reviewer must differ from the creator.</summary>
    public void MoveUnderReview(Guid reviewerId, DateTime occurredOn)
    {
        if (Status != RecordStatus.Submitted)
            throw new InvalidRecordTransitionException(Id, Status.Name, RecordStatus.UnderReview.Name);

        if (reviewerId == CreatedBy)
            throw new CreatorVerificationConflictException(Id);

        Status = RecordStatus.UnderReview;
        UpdatedBy = reviewerId;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new RecordUnderReviewEvent(Id, reviewerId));
    }

    /// <summary>
    /// Transitions <c>Under Review → Verified</c>. Freezes the working field set
    /// into the authoritative baseline (<c>VersionNumber = 1</c> if none exists).
    /// The verifier must differ from the creator (separation of duties).
    /// </summary>
    public void Verify(Guid verifiedBy, DateTime occurredOn)
    {
        if (Status != RecordStatus.UnderReview)
            throw new InvalidRecordTransitionException(Id, Status.Name, RecordStatus.Verified.Name);

        if (verifiedBy == CreatedBy)
            throw new CreatorVerificationConflictException(Id);

        var supersedes = _versions.Count == 0 ? null : (int?)_versions.Max(v => v.VersionNumber);
        var version = new RecordVersion(
            Guid.NewGuid(),
            _versions.Count == 0 ? 1 : _versions.Max(v => v.VersionNumber) + 1,
            _workingFields.ToArray(),
            supersedes,
            verifiedBy,
            occurredOn.ToUniversalTime(),
            changeReason: null);

        _versions.Add(version);
        CurrentVersionId = version.Id;
        Status = RecordStatus.Verified;
        VerifiedBy = verifiedBy;
        VerifiedOn = occurredOn.ToUniversalTime();
        UpdatedBy = verifiedBy;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new RecordVerifiedEvent(Id, verifiedBy));
    }

    /// <summary>Transitions <c>Under Review → Rejected</c>. The rejecting subject must differ from the creator.</summary>
    public void Reject(Guid rejectedBy, DateTime occurredOn)
    {
        if (Status != RecordStatus.UnderReview)
            throw new InvalidRecordTransitionException(Id, Status.Name, RecordStatus.Rejected.Name);

        if (rejectedBy == CreatedBy)
            throw new CreatorVerificationConflictException(Id);

        Status = RecordStatus.Rejected;
        UpdatedBy = rejectedBy;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new RecordRejectedEvent(Id, rejectedBy));
    }

    /// <summary>
    /// Applies a post-verification correction: appends a new superseding
    /// <see cref="RecordVersion"/> and moves the current-version pointer. The
    /// authoritative baseline is never mutated in place. Requires a change reason.
    /// </summary>
    public void Correct(
        IReadOnlyList<RecordFieldValue> fields,
        string changeReason,
        Guid correctedBy,
        DateTime occurredOn)
    {
        ValidateFields(fields);

        if (Status != RecordStatus.Verified)
            throw new InvalidRecordTransitionException(Id, Status.Name, "correct");

        if (string.IsNullOrWhiteSpace(changeReason))
            throw new CorrectionChangeReasonRequiredException(Id);

        var version = new RecordVersion(
            Guid.NewGuid(),
            _versions.Max(v => v.VersionNumber) + 1,
            fields,
            CurrentVersion?.VersionNumber,
            correctedBy,
            occurredOn.ToUniversalTime(),
            changeReason.Trim());

        _versions.Add(version);
        CurrentVersionId = version.Id;
        UpdatedBy = correctedBy;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new RecordCorrectedEvent(
            Id, version.VersionNumber, version.SupersedesVersionNumber, correctedBy));
    }

    /// <summary>Transitions <c>Verified → Archived</c> (read-only retention).</summary>
    public void Archive(Guid actorId, DateTime occurredOn)
    {
        if (Status != RecordStatus.Verified)
            throw new InvalidRecordTransitionException(Id, Status.Name, RecordStatus.Archived.Name);

        Status = RecordStatus.Archived;
        UpdatedBy = actorId;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new RecordArchivedEvent(Id));
    }

    /// <summary>
    /// Transitions <c>Verified/Archived → Deactivated</c> (reversible soft-delete).
    /// An active legal/administrative hold blocks deactivation unless an
    /// explicitly authorized administrative override with a reason is used
    /// (ADR-023, ratified).
    /// </summary>
    public void Deactivate(Guid actorId, bool adminOverride, string? reason, DateTime occurredOn)
    {
        if (Status != RecordStatus.Verified && Status != RecordStatus.Archived)
            throw new InvalidRecordTransitionException(Id, Status.Name, RecordStatus.Deactivated.Name);

        if (HasActiveHold)
        {
            if (!adminOverride)
                throw new HeldRecordDeactivationException(Id);
            if (string.IsNullOrWhiteSpace(reason))
                throw new AdminOverrideReasonRequiredException(Id);
        }

        Status = RecordStatus.Deactivated;
        UpdatedBy = actorId;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new RecordDeactivatedEvent(Id));
    }

    /// <summary>Restores <c>Archived</c>/<c>Deactivated</c> → <c>Verified</c>.</summary>
    public void Restore(Guid actorId, DateTime occurredOn)
    {
        if (Status != RecordStatus.Archived && Status != RecordStatus.Deactivated)
            throw new InvalidRecordTransitionException(Id, Status.Name, RecordStatus.Verified.Name);

        Status = RecordStatus.Verified;
        UpdatedBy = actorId;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new RecordRestoredEvent(Id, Status.Name));
    }

    /// <summary>
    /// Sets classification/sensitive/retention metadata. Metadata changes never
    /// create versions; the change is audited through events. Raises
    /// <c>RecordClassified</c>, and <c>RecordRetentionChanged</c> when the
    /// retention schedule reference actually changes.
    /// </summary>
    public void SetClassification(
        string? classificationCode,
        bool isSensitive,
        string? retentionScheduleCode,
        Guid classifiedBy,
        DateTime occurredOn)
    {
        if (Status == RecordStatus.Deactivated)
            throw new InvalidRecordTransitionException(Id, Status.Name, "classify");

        var retentionChanged =
            !string.Equals(Classification.RetentionScheduleCode, retentionScheduleCode, StringComparison.Ordinal);

        Classification.Classify(
            classificationCode,
            isSensitive,
            retentionScheduleCode,
            classifiedBy,
            occurredOn);

        UpdatedBy = classifiedBy;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new RecordClassifiedEvent(
            Id, Classification.ClassificationCode, Classification.IsSensitive));

        if (retentionChanged)
            RaiseDomainEvent(new RecordRetentionChangedEvent(
                Id, Classification.RetentionScheduleCode, null));
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

        _scopes.Add(new RecordOrganizationScope(Guid.NewGuid(), organizationUnitId));
        UpdatedBy = updatedBy;
        UpdatedOn = occurredOn.ToUniversalTime();
    }

    /// <summary>Removes an organization scope. Scope changes are metadata changes.</summary>
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
    }

    /// <summary>Attaches a document version as evidence. Idempotent per (document, version, type).</summary>
    public RecordEvidenceReference AttachEvidence(
        Guid documentId,
        int versionNumber,
        string referenceType,
        Guid attachedBy,
        DateTime attachedOn)
    {
        Guard.NotDefault(documentId, nameof(documentId));
        Guard.NotNullOrWhiteSpace(referenceType, nameof(referenceType));
        Guard.MaxLength(referenceType, 50, nameof(referenceType));
        EnsureCanEditMetadata();

        if (versionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be > 0.");

        var existing = _evidence.FirstOrDefault(e =>
            e.DocumentId == documentId &&
            e.VersionNumber == versionNumber &&
            string.Equals(e.ReferenceType, referenceType, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var evidence = new RecordEvidenceReference(
            Guid.NewGuid(),
            documentId,
            versionNumber,
            referenceType.Trim().ToLowerInvariant(),
            attachedBy,
            attachedOn.ToUniversalTime());

        _evidence.Add(evidence);
        UpdatedBy = attachedBy;
        UpdatedOn = attachedOn.ToUniversalTime();

        RaiseDomainEvent(new RecordEvidenceAttachedEvent(Id, documentId, versionNumber, evidence.ReferenceType));

        return evidence;
    }

    /// <summary>Removes an evidence reference.</summary>
    public void RemoveEvidence(Guid evidenceId, Guid removedBy, DateTime occurredOn)
    {
        var evidence = _evidence.FirstOrDefault(e => e.Id == evidenceId)
            ?? throw new RecordEvidenceNotFoundException(evidenceId);

        _evidence.Remove(evidence);
        UpdatedBy = removedBy;
        UpdatedOn = occurredOn.ToUniversalTime();

        RaiseDomainEvent(new RecordEvidenceRemovedEvent(Id, evidence.DocumentId, evidence.VersionNumber));
    }

    /// <summary>
    /// Places a legal/administrative hold on the record. While active the hold
    /// blocks deactivation of the record (and of referenced held documents, via
    /// the Documents classify surface). The reason is sensitive. Only the
    /// ratified <c>legal</c> | <c>administrative</c> hold types are accepted.
    /// </summary>
    public RecordHold PlaceHold(
        string holdType,
        string reason,
        IReadOnlyList<RecordHoldDocumentReference>? documentReferences,
        Guid placedBy,
        DateTime placedOn)
    {
        Guard.NotNullOrWhiteSpace(holdType, nameof(holdType));
        Guard.MaxLength(holdType, 30, nameof(holdType));
        Guard.NotNullOrWhiteSpace(reason, nameof(reason));
        Guard.MaxLength(reason, 2000, nameof(reason));

        if (!string.Equals(holdType, RecordHoldTypes.Legal, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(holdType, RecordHoldTypes.Administrative, StringComparison.OrdinalIgnoreCase))
            throw new InvalidRecordHoldTypeException(holdType);

        EnsureCanEditMetadata();

        var hold = new RecordHold(
            Guid.NewGuid(),
            holdType.Trim().ToLowerInvariant(),
            reason.Trim(),
            placedBy,
            placedOn.ToUniversalTime(),
            documentReferences);

        _holds.Add(hold);
        UpdatedBy = placedBy;
        UpdatedOn = placedOn.ToUniversalTime();

        RaiseDomainEvent(new RecordHoldPlacedEvent(hold.Id, Id, hold.HoldType, placedBy));

        return hold;
    }

    /// <summary>
    /// Releases an active hold. The releaser must differ from the placer
    /// (separation of duties, ADR-023).
    /// </summary>
    public void ReleaseHold(Guid holdId, Guid releasedBy, DateTime releasedOn)
    {
        var hold = _holds.FirstOrDefault(h => h.Id == holdId)
            ?? throw new RecordHoldNotFoundException(holdId);

        hold.Release(releasedBy, releasedOn);
        UpdatedBy = releasedBy;
        UpdatedOn = releasedOn.ToUniversalTime();

        RaiseDomainEvent(new RecordHoldReleasedEvent(hold.Id, Id, hold.HoldType, releasedBy));
    }

    /// <summary>
    /// Flags the record for retention disposition review when its retention
    /// period lapses. Retention expiry never destroys data (ADR-023).
    /// </summary>
    public void FlagRetentionExpired(DateTime expiredOn)
    {
        if (Status == RecordStatus.Deactivated)
            throw new InvalidRecordTransitionException(Id, Status.Name, "retention expiry");

        var scheduleCode = Classification.RetentionScheduleCode;
        if (string.IsNullOrWhiteSpace(scheduleCode))
            throw new RetentionScheduleNotFoundException(string.Empty);

        Classification.FlagRetentionExpired(expiredOn);

        RaiseDomainEvent(new RecordRetentionExpiredEvent(Id, scheduleCode, expiredOn));
    }

    private void EnsureCanEditMetadata()
    {
        if (Status == RecordStatus.Archived)
            throw new InvalidRecordTransitionException(Id, Status.Name, "metadata update");
        if (Status == RecordStatus.Deactivated)
            throw new InvalidRecordTransitionException(Id, Status.Name, "metadata update");
    }

    private static void ValidateFields(IReadOnlyList<RecordFieldValue> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            Guard.NotNullOrWhiteSpace(field.FieldKey, nameof(RecordFieldValue.FieldKey));
            Guard.MaxLength(field.FieldKey, 100, nameof(RecordFieldValue.FieldKey));
            Guard.NotNullOrWhiteSpace(field.FieldValue, nameof(RecordFieldValue.FieldValue));
            Guard.MaxLength(field.FieldValue, 8000, nameof(RecordFieldValue.FieldValue));

            if (!seen.Add(field.FieldKey))
                throw new ArgumentException($"Duplicate field key '{field.FieldKey}'.", nameof(fields));
        }
    }
}