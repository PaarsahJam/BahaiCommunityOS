using CommunityOS.Correspondence.Domain.Exceptions;

namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// The letter aggregate (ADR-028 decision 1): identity and per-unit yearly
/// reference number, working content (subject/body/category/sensitivity),
/// organization scope, recipient references with external addressing
/// snapshots, the lifecycle state machine with its immutable transition
/// history, materialization correlation state and retention classification.
///
/// Post-submit corrections are new letters linked through
/// <see cref="RelatedLetterId"/>; submitted history is never rewritten. The
/// aggregate enforces every legal transition locally; the database trigger
/// guard provides the storage-level second layer of immutability.
/// </summary>
public sealed class Letter
{
    private readonly List<LetterRecipient> _recipients = [];
    private readonly List<LetterStatusHistory> _history = [];
    private readonly List<LetterDocumentLink> _documentLinks = [];
    private readonly List<LetterAttachment> _attachments = [];

    private Letter()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrganizationUnitId { get; private set; }

    /// <summary>Bounded category code (e.g. "official").</summary>
    public string CategoryCode { get; private set; } = null!;

    /// <summary>Working subject; never leaves the service in events, lists,
    /// exports or logs.</summary>
    public string Subject { get; private set; } = null!;

    /// <summary>Working body; the retained working copy of the submitted text.</summary>
    public string Body { get; private set; } = null!;

    public LetterSensitivity Sensitivity { get; private set; }

    public LetterStatus Status { get; private set; }

    /// <summary>Optimistic-concurrency revision; bumped on every mutation.</summary>
    public int Revision { get; private set; }

    public int? LetterYear { get; private set; }

    public int? LetterSequence { get; private set; }

    /// <summary>Template snapshot provenance: the template a draft was created
    /// from. Edits to the template never mutate existing letters.</summary>
    public Guid? TemplateId { get; private set; }

    public string? TemplateCode { get; private set; }

    /// <summary>Correction-chain link: the earlier letter this one corrects or
    /// supersedes (set at creation only).</summary>
    public Guid? RelatedLetterId { get; private set; }

    /// <summary>The person the letter is about, when known (reference only).</summary>
    public Guid? SubjectPersonId { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public Guid? SubmittedBy { get; private set; }

    public DateTime? SubmittedOn { get; private set; }

    public DateTime? MaterializedOn { get; private set; }

    public DateTime? DispatchedOn { get; private set; }

    public DateTime? DeliveredOn { get; private set; }

    public string? DeliveryFailureReasonCode { get; private set; }

    public Guid? CancelledBy { get; private set; }

    public DateTime? CancelledOn { get; private set; }

    public string? CancellationReasonCode { get; private set; }

    public string RetentionClass { get; private set; } = null!;

    public DateTime? RetentionExpiresOn { get; private set; }

    public IReadOnlyList<LetterRecipient> Recipients => _recipients;

    /// <summary>Lifecycle rows accumulated by this aggregate since it was
    /// loaded/created. Deliberately NOT a persistence navigation: the journal
    /// drains these rows into the FK-less history table so purge tombstones
    /// survive letter deletion (ADR-028 decision 13).</summary>
    public IReadOnlyList<LetterStatusHistory> DrainPendingHistory()
    {
        var drained = _history.ToArray();
        _history.Clear();
        return drained;
    }

    public IReadOnlyList<LetterDocumentLink> DocumentLinks => _documentLinks;

    public IReadOnlyList<LetterAttachment> Attachments => _attachments;

    /// <summary>The ratified display reference, composed by the API layer:
    /// <c>{letter_year}-{sequence:D5}</c> scoped by unit.</summary>
    public string? ReferenceNumber =>
        LetterYear is { } year && LetterSequence is { } sequence
            ? $"{year}-{sequence:D5}"
            : null;

    public static Letter CreateDraft(
        Guid organizationUnitId,
        string categoryCode,
        string subject,
        string body,
        LetterSensitivity sensitivity,
        Guid createdBy,
        DateTime now,
        Guid? templateId = null,
        string? templateCode = null,
        Guid? relatedLetterId = null,
        Guid? subjectPersonId = null)
    {
        if (organizationUnitId == Guid.Empty)
        {
            throw new ArgumentException("Organization unit is required.", nameof(organizationUnitId));
        }

        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            throw new ArgumentException("Category code is required.", nameof(categoryCode));
        }

        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("Creator is required.", nameof(createdBy));
        }

        var letter = new Letter
        {
            Id = Guid.NewGuid(),
            OrganizationUnitId = organizationUnitId,
            CategoryCode = categoryCode.Trim(),
            Subject = (subject ?? string.Empty).Trim(),
            Body = body ?? string.Empty,
            Sensitivity = sensitivity,
            Status = LetterStatus.Draft,
            Revision = 1,
            CreatedBy = createdBy,
            CreatedOn = now,
            UpdatedOn = now,
            TemplateId = templateId,
            TemplateCode = templateCode,
            RelatedLetterId = relatedLetterId,
            SubjectPersonId = subjectPersonId
        };
        letter.AppendHistory(LetterStatus.Draft, HistoryCause.Command, createdBy, null, now);
        return letter;
    }

    public LetterRecipient AddRecipient(
        RecipientKind kind, Guid? personId, Guid? unitId, string? displayLine, DateTime now)
    {
        EnsureEditable("recipients may only be changed while the letter is a draft.");
        var recipient = LetterRecipient.Create(Id, kind, personId, unitId, displayLine, now);
        if (_recipients.Any(r => r.Equals(recipient)))
        {
            throw new LetterConflictException("An identical recipient already exists on this letter.");
        }

        _recipients.Add(recipient);
        Touch(now);
        return recipient;
    }

    public void RemoveRecipient(Guid recipientId, DateTime now)
    {
        EnsureEditable("recipients may only be changed while the letter is a draft.");
        var recipient = _recipients.FirstOrDefault(r => r.Id == recipientId)
            ?? throw new LetterNotFoundException();
        _recipients.Remove(recipient);
        Touch(now);
    }

    /// <summary>Draft content edit; bumps the optimistic-concurrency revision.</summary>
    public void UpdateContent(string subject, string body, DateTime now)
    {
        EnsureEditable("content may only be edited while the letter is a draft.");
        Subject = (subject ?? string.Empty).Trim();
        Body = body ?? string.Empty;
        Touch(now);
    }

    public void Confirm(Guid actorId, DateTime now)
    {
        EnsureStatus(LetterStatus.Draft, "Only draft letters can be confirmed.");
        if (string.IsNullOrWhiteSpace(Subject) || string.IsNullOrWhiteSpace(Body))
        {
            throw new LetterConflictException("A letter requires a subject and a body before confirmation.");
        }

        if (_recipients.Count == 0)
        {
            throw new LetterConflictException("A letter requires at least one recipient before confirmation.");
        }

        Transition(LetterStatus.Confirmed, HistoryCause.Command, actorId, null, now);
    }

    public void Unconfirm(Guid actorId, DateTime now)
    {
        EnsureStatus(LetterStatus.Confirmed, "Only confirmed letters can revert to draft.");
        Transition(LetterStatus.Draft, HistoryCause.Command, actorId, null, now);
    }

    /// <summary>Submission transition. The reference number itself is allocated
    /// transactionally by the persistence layer (per-unit yearly uniqueness);
    /// this method validates legality, applies the number, appends history and
    /// stamps the retention classification computed at submit.</summary>
    public void Submit(
        Guid submittedBy, int letterYear, int letterSequence,
        string retentionClass, DateTime? retentionExpiresOn, DateTime now)
    {
        EnsureStatus(LetterStatus.Confirmed, "Only confirmed letters can be submitted.");
        if (letterSequence < 1)
        {
            throw new ArgumentException("Letter sequence must be positive.", nameof(letterSequence));
        }

        SubmittedBy = submittedBy;
        SubmittedOn = now;
        RetentionClass = retentionClass;
        RetentionExpiresOn = retentionExpiresOn;
        LetterYear = letterYear;
        LetterSequence = letterSequence;
        Transition(LetterStatus.Submitted, HistoryCause.Command, submittedBy, null, now);
    }

    /// <summary>Event-driven materialization edge: the correlated Documents
    /// version-added fact closes the loop (cause = Event). Replays of the same
    /// correlation are no-ops regardless of current status (idempotent
    /// convergence); a new correlation is legal only while Submitted.</summary>
    public void MarkMaterialized(Guid documentId, int versionNumber, string contentHash, DateTime now)
    {
        if (_documentLinks.Any(l => l.DocumentId == documentId))
        {
            return; // idempotent replay of the same correlation
        }

        EnsureStatus(LetterStatus.Submitted, "Only submitted letters can be materialized.");

        _documentLinks.Add(
            LetterDocumentLink.Create(Id, documentId, versionNumber, contentHash, now));
        MaterializedOn = now;
        Transition(LetterStatus.Materialized, HistoryCause.Event, actorId: null, reasonCode: null, now);
    }

    /// <summary>Manual dispatch recording (first gate has no providers).
    /// Dispatch remains blocked until materialization has been confirmed.</summary>
    public void RecordDispatch(string methodCode, Guid recordedBy, DateTime now)
    {
        EnsureStatus(LetterStatus.Materialized, "Only materialized letters can be dispatched.");
        LastDispatchMethodCode = methodCode.Trim();
        DeliveryRecordsInternal.Add(LetterDeliveryRecord.Create(
            Id, methodCode, DeliveryOutcome.Dispatched, null, recordedBy, now));
        DispatchedOn = now;
        Transition(LetterStatus.Dispatched, HistoryCause.Command, recordedBy, null, now);
    }

    public void ConfirmDelivery(string methodCode, Guid recordedBy, DateTime now)
    {
        EnsureStatus(LetterStatus.Dispatched, "Only dispatched letters can confirm delivery.");
        DeliveredOn = now;
        DeliveryRecordsInternal.Add(LetterDeliveryRecord.Create(
            Id, methodCode, DeliveryOutcome.Confirmed, null, recordedBy, now));
        Transition(LetterStatus.Delivered, HistoryCause.Command, recordedBy, null, now);
    }

    public void FailDelivery(string methodCode, string reasonCode, Guid recordedBy, DateTime now)
    {
        EnsureStatus(LetterStatus.Dispatched, "Only dispatched letters can record delivery failure.");
        if (!CorrespondenceReasonCodes.IsKnown(CorrespondenceReasonCodes.DeliveryFailure, reasonCode))
        {
            throw new ArgumentException($"Unknown delivery-failure reason code '{reasonCode}'.", nameof(reasonCode));
        }

        DeliveryFailureReasonCode = reasonCode;
        DeliveryRecordsInternal.Add(LetterDeliveryRecord.Create(
            Id, methodCode, DeliveryOutcome.Failed, reasonCode, recordedBy, now));
        Transition(LetterStatus.DeliveryFailed, HistoryCause.Command, recordedBy, reasonCode, now);
    }

    /// <summary>Cancellation is legal only pre-dispatch (ADR-028 decision 3);
    /// the consumed reference number is retained permanently.</summary>
    public void Cancel(Guid cancelledBy, string reasonCode, DateTime now)
    {
        if (!CorrespondenceReasonCodes.IsKnown(CorrespondenceReasonCodes.Cancellation, reasonCode))
        {
            throw new ArgumentException($"Unknown cancellation reason code '{reasonCode}'.", nameof(reasonCode));
        }

        var legal = Status is LetterStatus.Draft
            or LetterStatus.Confirmed
            or LetterStatus.Submitted
            or LetterStatus.Materialized;
        if (!legal)
        {
            throw new LetterConflictException($"Letters cannot be cancelled from status {Status}.");
        }

        CancelledBy = cancelledBy;
        CancelledOn = now;
        CancellationReasonCode = reasonCode;
        Transition(LetterStatus.Cancelled, HistoryCause.Command, cancelledBy, reasonCode, now);
    }

    /// <summary>Purge tombstone: appended inside the guarded purge transaction
    /// before the batch deletion so the audit trail ends with the marker.</summary>
    public LetterStatusHistory BuildPurgeTombstone(DateTime now) =>
        LetterStatusHistory.CreateTombstone(Id, Status, now);

    // ---- Internals -----------------------------------------------------------

    /// <summary>Delivery recordings are aggregate children managed through the
    /// outcome methods; exposed internally for EF mapping.</summary>
    internal List<LetterDeliveryRecord> DeliveryRecordsInternal { get; } = [];

    /// <summary>Method code recorded with the dispatch; reused for delivery
    /// outcome records and integration events.</summary>
    public string? LastDispatchMethodCode { get; private set; }

    private void Touch(DateTime now)
    {
        Revision++;
        UpdatedOn = now;
    }

    private void Transition(
        LetterStatus to, HistoryCause cause, Guid? actorId, string? reasonCode, DateTime now)
    {
        var from = Status;
        Status = to;
        AppendHistory(to, cause, actorId, reasonCode, now, from);
        Revision++;
        UpdatedOn = now;
    }

    private void AppendHistory(
        LetterStatus to, HistoryCause cause, Guid? actorId, string? reasonCode,
        DateTime now, LetterStatus? from = null)
    {
        _history.Add(LetterStatusHistory.Create(Id, from ?? Status, to, cause, actorId, reasonCode, now));
    }

    private void EnsureEditable(string message)
    {
        if (Status != LetterStatus.Draft)
        {
            throw new LetterConflictException($"The letter is not editable: {message}");
        }
    }

    private void EnsureStatus(LetterStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new LetterConflictException($"{message} Current status: {Status}.");
        }
    }
}
