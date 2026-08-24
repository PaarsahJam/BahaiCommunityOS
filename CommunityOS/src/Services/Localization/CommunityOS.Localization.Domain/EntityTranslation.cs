using CommunityOS.Localization.Domain.Exceptions;

namespace CommunityOS.Localization.Domain;

/// <summary>
/// A by-reference translation for a display field of a record owned by
/// another bounded context (ADR-029 decisions 1 and 5). The canonical value
/// never leaves the owning context; Localization stores only the translated
/// display value and its review lifecycle. References are opaque stable ids â€”
/// no foreign keys or navigation properties cross service databases.
/// </summary>
public sealed class EntityTranslation
{
    private readonly List<EntityTranslationRevision> _revisions = [];

    private EntityTranslation()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Which bounded context owns the source entity (e.g. "community").</summary>
    public string SourceContext { get; private set; } = null!;

    /// <summary>Entity type inside the owning context (e.g. "person").</summary>
    public string EntityType { get; private set; } = null!;

    /// <summary>Opaque stable id of the source entity in the owning context.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>The display field being translated (e.g. "display_name").</summary>
    public string Field { get; private set; } = null!;

    /// <summary>BCP-47 code of the culture of the stored value.</summary>
    public string CultureCode { get; private set; } = null!;

    public bool IsDeprecated { get; private set; }

    public DateTime? DeprecatedOn { get; private set; }

    public int Revision { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public IReadOnlyList<EntityTranslationRevision> Revisions => _revisions;

    public static EntityTranslation Create(
        string sourceContext, string entityType, Guid entityId,
        string field, string culture, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(sourceContext) ||
            sourceContext.Trim().Length is < 2 or > 50 ||
            !sourceContext.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException("A source context of 2â€“50 letters/digits/underscores is required.");
        }

        if (string.IsNullOrWhiteSpace(entityType) ||
            entityType.Trim().Length is < 2 or > 100 ||
            !entityType.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
        {
            throw new ArgumentException("An entity type of 2â€“100 letters/digits/underscores/hyphens is required.");
        }

        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("The source entity id is required.");
        }

        if (string.IsNullOrWhiteSpace(field) ||
            field.Trim().Length is < 2 or > 100 ||
            !field.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
        {
            throw new ArgumentException("A field name of 2â€“100 letters/digits/underscores/hyphens is required.");
        }

        if (!Bcp47.IsValid(culture))
        {
            throw new ArgumentException($"'{culture}' is not a valid BCP-47 culture code.");
        }

        ResourceNamespace.EnsureSourceContextAllowed(sourceContext);

        return new EntityTranslation
        {
            Id = Guid.NewGuid(),
            SourceContext = sourceContext.Trim().ToLowerInvariant(),
            EntityType = entityType.Trim().ToLowerInvariant(),
            EntityId = entityId,
            Field = field.Trim().ToLowerInvariant(),
            CultureCode = Bcp47.Normalize(culture),
            Revision = 1,
            CreatedOn = now,
            UpdatedOn = now
        };
    }

    public EntityTranslationRevision ProposeDraft(
        string value, ContentProvenance provenance, Guid proposedBy, DateTime now)
    {
        EnsureNotDeprecated();
        var revision = EntityTranslationRevision.Draft(Id, CultureCode, value, provenance, proposedBy, now);
        _revisions.Add(revision);
        Touch(now);
        return revision;
    }

    public void SubmitForReview(Guid revisionId, DateTime now)
    {
        FindRevision(revisionId).Transition(ReviewState.InReview, now);
        Touch(now);
    }

    /// <summary>Approves a revision; a previously approved revision is
    /// superseded verbatim. The handler publishes the catalog-change event
    /// before the journal's single save.</summary>
    public void Approve(Guid revisionId, Guid reviewer, DateTime now)
    {
        var revision = FindRevision(revisionId);
        revision.Transition(ReviewState.Approved, now);
        revision.MarkReviewed(reviewer, now);

        foreach (var other in _revisions.Where(r =>
                     r.Id != revisionId && r.State == ReviewState.Approved))
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
            throw new LocalizationConflictException("The entity translation is already deprecated.");
        }

        IsDeprecated = true;
        DeprecatedOn = now;
        Touch(now);
    }

    public EntityTranslationRevision? CurrentApproved() =>
        _revisions.FirstOrDefault(r => r.State == ReviewState.Approved);

    private void EnsureNotDeprecated()
    {
        if (IsDeprecated)
        {
            throw new LocalizationConflictException(
                "The entity translation is deprecated; re-create it if the owning context needs it again.");
        }
    }

    private EntityTranslationRevision FindRevision(Guid revisionId) =>
        _revisions.FirstOrDefault(r => r.Id == revisionId)
        ?? throw new LocalizationNotFoundException("The requested translation revision was not found.");

    private void Touch(DateTime now) => UpdatedOn = now;
}
