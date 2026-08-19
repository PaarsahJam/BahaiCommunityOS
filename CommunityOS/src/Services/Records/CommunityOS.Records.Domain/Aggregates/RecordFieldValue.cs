using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Records.Domain.Aggregates;

/// <summary>
/// A single typed field value on a record version (ADR-023). Facts are stored
/// as typed rows — never as a JSON blob — so schema evolution and sensitive-field
/// gating stay first-class. Every field is immutable once the version is written.
/// </summary>
public sealed class RecordFieldValue : Entity<Guid>
{
    private RecordFieldValue() : base(Guid.Empty)
    {
        FieldKey = null!;
        FieldValue = null!;
    }

    internal RecordFieldValue(Guid id, string fieldKey, string fieldValue, bool isSensitive) : base(id)
    {
        FieldKey = fieldKey;
        FieldValue = fieldValue;
        IsSensitive = isSensitive;
    }

    /// <summary>Creates a working field value for a record. Ids are service-assigned.</summary>
    public static RecordFieldValue Create(string fieldKey, string fieldValue, bool isSensitive) =>
        new(Guid.NewGuid(), fieldKey, fieldValue, isSensitive);

    public string FieldKey { get; private set; }

    public string FieldValue { get; private set; }

    public bool IsSensitive { get; private set; }
}