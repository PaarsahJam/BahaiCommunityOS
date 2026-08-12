using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Organization.Domain.ValueObjects;

/// <summary>
/// Describes the administrative scope of an organization, committee or unit:
/// a jurisdiction type plus an optional scope id. A jurisdiction scoped to a
/// parent organization unit participates in hierarchy-aware authorization via
/// the Authorization integration boundary.
/// </summary>
public sealed class Jurisdiction : ValueObject
{
    private Jurisdiction(JurisdictionType type, Guid? scopeId)
    {
        Type = type;
        ScopeId = scopeId;
    }

    public JurisdictionType Type { get; }
    public Guid? ScopeId { get; }

    public bool IsGlobal => Type == JurisdictionType.Global;

    public static Jurisdiction Global() => new(JurisdictionType.Global, null);

    public static Jurisdiction Create(JurisdictionType type, Guid? scopeId = null)
    {
        Guard.NotNull(type, nameof(type));

        if (type == JurisdictionType.Global)
            return Global();

        Guard.NotDefault(scopeId ?? Guid.Empty, nameof(scopeId));
        return new Jurisdiction(type, scopeId);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return ScopeId;
    }

    public override string ToString() =>
        IsGlobal ? Type.Name : Type.Name + ":" + ScopeId;
}
