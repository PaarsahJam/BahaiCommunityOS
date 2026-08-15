using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Documents.Domain.Enumerations;

/// <summary>
/// Lifecycle state of a document:
/// <c>Draft → Active → Archived | Deactivated</c> (ADR-022).
/// <c>Active</c> is entered automatically when the first version is uploaded;
/// <c>Archived</c> is read-only retention; <c>Deactivated</c> is a reversible
/// soft-delete. Restoration returns an <c>Archived</c>/<c>Deactivated</c>
/// document to <c>Active</c>.
/// </summary>
public sealed class DocumentStatus : Enumeration<int>
{
    public static readonly DocumentStatus Draft = new(1, "draft");
    public static readonly DocumentStatus Active = new(2, "active");
    public static readonly DocumentStatus Archived = new(3, "archived");
    public static readonly DocumentStatus Deactivated = new(4, "deactivated");

    private DocumentStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<DocumentStatus> All =>
        [Draft, Active, Archived, Deactivated];

    public static DocumentStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown DocumentStatus id: {id}");

    public static DocumentStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown DocumentStatus name: {name}");
}