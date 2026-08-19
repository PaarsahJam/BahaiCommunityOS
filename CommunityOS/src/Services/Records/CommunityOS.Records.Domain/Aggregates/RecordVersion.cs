using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Records.Domain.Aggregates;

/// <summary>
/// An immutable snapshot of the authoritative record facts (ADR-023). Every
/// field is immutable once written. Version numbers are 1-based, monotonically
/// increasing per record, and assigned by the service — never client-supplied.
/// A <c>Draft</c>/<c>Submitted</c>/<c>Under Review</c> record holds a working
/// field set; the first <c>Verified</c> transition freezes those facts into the
/// authoritative baseline (<c>VersionNumber = 1</c>). Each post-verification
/// correction appends a new superseding version; prior versions are never
/// mutated.
/// </summary>
public sealed class RecordVersion : Entity<Guid>
{
    private readonly List<RecordFieldValue> _fields = [];

    private RecordVersion() : base(Guid.Empty)
    {
    }

    internal RecordVersion(
        Guid id,
        int versionNumber,
        IReadOnlyList<RecordFieldValue> fields,
        int? supersedesVersionNumber,
        Guid appliedBy,
        DateTime appliedOn,
        string? changeReason) : base(id)
    {
        VersionNumber = versionNumber;
        _fields.AddRange(fields);
        SupersedesVersionNumber = supersedesVersionNumber;
        AppliedBy = appliedBy;
        AppliedOn = appliedOn;
        ChangeReason = changeReason;
    }

    public int VersionNumber { get; private set; }

    public int? SupersedesVersionNumber { get; private set; }

    public Guid AppliedBy { get; private set; }

    public DateTime AppliedOn { get; private set; }

    /// <summary>Required when this is a post-verification correction. Never exported in events.</summary>
    public string? ChangeReason { get; private set; }

    public IReadOnlyList<RecordFieldValue> Fields => _fields.AsReadOnly();

    public bool IsCorrection => SupersedesVersionNumber is not null;
}