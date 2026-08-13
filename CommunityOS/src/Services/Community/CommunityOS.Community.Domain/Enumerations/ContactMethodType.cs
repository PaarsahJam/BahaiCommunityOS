using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Community.Domain.Enumerations;

/// <summary>
/// Type of a contact method on a person record. Values are structurally
/// validated when the contact method is created.
/// </summary>
public sealed class ContactMethodType : Enumeration<int>
{
    public static readonly ContactMethodType Email = new(1, "email");
    public static readonly ContactMethodType Phone = new(2, "phone");
    public static readonly ContactMethodType Postal = new(3, "postal");

    private ContactMethodType(int id, string name) : base(id, name) { }

    public static IEnumerable<ContactMethodType> All => [Email, Phone, Postal];

    public static ContactMethodType FromId(int id) =>
        All.FirstOrDefault(t => t.Id == id)
        ?? throw new ArgumentException($"Unknown ContactMethodType id: {id}");

    public static ContactMethodType FromName(string name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown ContactMethodType name: {name}");
}
