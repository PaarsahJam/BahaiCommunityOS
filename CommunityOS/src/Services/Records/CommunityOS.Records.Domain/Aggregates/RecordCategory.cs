using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Records.Domain.Aggregates;

/// <summary>
/// Catalog entry for a record category (ADR-023). Categories use stable string
/// codes (never renamed), so existing records keep their category reference.
/// A category can be retired (soft state) but is never deleted.
/// </summary>
public sealed class RecordCategory : AggregateRoot<Guid>
{
    private RecordCategory() : base(Guid.Empty)
    {
        Code = null!;
        DisplayName = null!;
    }

    internal RecordCategory(Guid id, string code, string displayName, string? description, Guid createdBy) : base(id)
    {
        Code = code;
        DisplayName = displayName;
        Description = description;
        CreatedBy = createdBy;
        CreatedOn = DateTime.UtcNow;
        IsRetired = false;
    }

    /// <summary>Stable string code referenced by <see cref="Record.Category"/>. Never renamed.</summary>
    public string Code { get; private set; }

    public string DisplayName { get; private set; }

    public string? Description { get; private set; }

    public bool IsRetired { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public static RecordCategory Create(string code, string displayName, string? description, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Category code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        return new RecordCategory(Guid.NewGuid(), code.Trim(), displayName.Trim(), description, createdBy);
    }

    public void Update(string displayName, string? description, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        DisplayName = displayName.Trim();
        Description = description;
        UpdatedBy = updatedBy;
        UpdatedOn = DateTime.UtcNow;
    }

    public void Retire(Guid retiredBy)
    {
        IsRetired = true;
        UpdatedBy = retiredBy;
        UpdatedOn = DateTime.UtcNow;
    }

    public void Reactivate(Guid reactivatedBy)
    {
        IsRetired = false;
        UpdatedBy = reactivatedBy;
        UpdatedOn = DateTime.UtcNow;
    }

    public Guid? UpdatedBy { get; private set; }

    public DateTime? UpdatedOn { get; private set; }
}