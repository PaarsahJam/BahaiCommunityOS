using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Finance.Domain.Aggregates;

/// <summary>
/// Cross-context read-model of an organization unit scope (ADR-016). The id
/// references the source <c>OrganizationUnit</c> row in the Organization
/// context (never a Finance foreign key); the display name is a denormalized
/// snapshot kept current by the <c>OrganizationIntegrationEventConsumer</c>
/// and must not be written by this context. Authorization is resolved live by
/// the Authorization service, never from this projection.
/// </summary>
public sealed class OrganizationUnitReference : Entity<Guid>
{
    private OrganizationUnitReference()
        : base(Guid.Empty) { }

    private OrganizationUnitReference(Guid id, Guid organizationUnitId, string displayName)
        : base(id)
    {
        OrganizationUnitId = organizationUnitId;
        DisplayName = displayName;
        CreatedOn = DateTime.UtcNow;
    }

    public Guid OrganizationUnitId { get; private set; }

    public string DisplayName { get; private set; } = null!;

    public DateTime CreatedOn { get; private set; }

    public static OrganizationUnitReference Create(Guid id, Guid organizationUnitId, string displayName) =>
        new(id, organizationUnitId, displayName);

    public void UpdateDisplayName(string displayName) => DisplayName = displayName;
}