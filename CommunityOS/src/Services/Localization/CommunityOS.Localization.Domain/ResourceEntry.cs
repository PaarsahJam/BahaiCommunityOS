using CommunityOS.Localization.Domain.Exceptions;

namespace CommunityOS.Localization.Domain;

/// <summary>
/// A key in the resource catalog (ADR-029 decision 4): the aggregate root that
/// owns the per-culture revisions of one UI string. Approved keys are never
/// renamed — deprecate and create a successor. The whole aggregate persists in
/// a single SaveChanges so outbox-captured catalog events commit atomically
/// (ADR-015, ADR-029 decisions 14/16).
/// </summary>
public sealed class ResourceEntry
{
    private readonly List<ResourceRevision> _revisions = [];

    private ResourceEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid NamespaceId { get; private set; }

    /// <summary>Immutable after creation; renaming an approved key is forbidden
    /// — deprecate and create a successor instead.</summary>
    public string Key { get; private set; } = null!;

    /// <summary>Key-level state: deprecated keys accept no new revisions.</summary>
    public bool IsDeprecated { get; private set; }

    public DateTime? DeprecatedOn { get; private set; }

    public int Revision { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public IReadOnlyList<ResourceRevision> Revisions => _revisions;

    public static ResourceEntry Create(Guid namespaceId, string key, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A resource key is required.");
        }

        var normalized = key.Trim();
        if (normalized.Length is < 1 or > 200 || !normalized.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ':'))
        {
            throw new ArgumentException(
                "Resource keys may contain ASCII letters, digits and '.', '_', '-', ':' only.");
        }

        return new ResourceEntry
        {
            Id = Guid.NewGuid(),
            NamespaceId = namespaceId,
            Key = normalized,
            CreatedOn = now,
            UpdatedOn = now
        };
    }

    public ResourceRevision ProposeDraft(
        string culture, string value, ContentProvenance provenance, Guid proposedBy, DateTime now)
    {
        EnsureNotDeprecated();
        var revision = ResourceRevision.Draft(Id, culture, value, provenance, proposedBy, now);
        _revisions.Add(revision);
        Touch(now);
        return revision;
    }

    public void SubmitForReview(Guid revisionId, DateTime now)
    {
        FindRevision(revisionId).Transition(ReviewState.InReview, now);
        Touch(now);
    }

    /// <summary>Approves a revision: immutable approval of this value for its
    /// culture. A previously approved revision for the same culture is marked
    /// Superseded (retained verbatim). The handler publishes the catalog-change
    /// domain event after this call and before the journal's single save, so
    /// the outbox row commits atomically with the approval.</summary>
    public void Approve(Guid revisionId, Guid reviewer, DateTime now)
    {
        var revision = FindRevision(revisionId);
        revision.Transition(ReviewState.Approved, now);
        revision.MarkReviewed(reviewer, now);

        foreach (var other in _revisions.Where(r =>
                     r.Id != revisionId &&
                     r.CultureCode == revision.CultureCode &&
                     r.State == ReviewState.Approved))
        {
            other.Transition(ReviewState.Superseded, now);
            other.MarkReviewed(reviewer, now);
        }

        Touch(now);
    }

    public void Reject(Guid revisionId, Guid reviewer, DateTime now)
    {
        var revision = FindRevision(revisionId);
        revision.Transition(ReviewState.Rejected, now);
        revision.MarkReviewed(reviewer, now);
        Touch(now);
    }

    public void Deprecate(DateTime now)
    {
        if (IsDeprecated)
        {
            throw new LocalizationConflictException("The resource entry is already deprecated.");
        }

        IsDeprecated = true;
        DeprecatedOn = now;
        Touch(now);
    }

    public ResourceRevision? CurrentApproved(string culture) =>
        _revisions.FirstOrDefault(r => r.CultureCode == culture && r.State == ReviewState.Approved);

    public bool HasCulture(string culture) => _revisions.Any(r => r.CultureCode == culture);

    private void EnsureNotDeprecated()
    {
        if (IsDeprecated)
        {
            throw new LocalizationConflictException(
                "The resource entry is deprecated; create a successor key instead.");
        }
    }

    private ResourceRevision FindRevision(Guid revisionId) =>
        _revisions.FirstOrDefault(r => r.Id == revisionId)
        ?? throw new LocalizationNotFoundException("The requested resource revision was not found.");

    private void Touch(DateTime now) => UpdatedOn = now;
}
