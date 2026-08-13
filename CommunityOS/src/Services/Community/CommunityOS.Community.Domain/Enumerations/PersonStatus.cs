using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a person record. A person is never hard-deleted:
/// deactivation is a state change that preserves historical participation.
/// </summary>
public sealed class PersonStatus : Enumeration<int>
{
    public static readonly PersonStatus Active = new(1, "active");
    public static readonly PersonStatus Deactivated = new(2, "deactivated");

    private PersonStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<PersonStatus> All => [Active, Deactivated];

    public static PersonStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown PersonStatus id: {id}");

    public static PersonStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown PersonStatus name: {name}");
}
