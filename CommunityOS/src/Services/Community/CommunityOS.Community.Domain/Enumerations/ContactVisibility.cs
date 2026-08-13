using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Visibility of a person's contact or profile attribute. The most private
/// level restricts exposure to explicitly permissioned actors; enforcement is
/// performed by the application layer through the Authorization guard.
/// </summary>
public sealed class ContactVisibility : Enumeration<int>
{
    public static readonly ContactVisibility Public = new(1, "public");
    public static readonly ContactVisibility Members = new(2, "members");
    public static readonly ContactVisibility Private = new(3, "private");

    private ContactVisibility(int id, string name) : base(id, name) { }

    public static IEnumerable<ContactVisibility> All => [Public, Members, Private];

    public static ContactVisibility FromId(int id) =>
        All.FirstOrDefault(v => v.Id == id)
        ?? throw new ArgumentException($"Unknown ContactVisibility id: {id}");

    public static ContactVisibility FromName(string name) =>
        All.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown ContactVisibility name: {name}");
}
