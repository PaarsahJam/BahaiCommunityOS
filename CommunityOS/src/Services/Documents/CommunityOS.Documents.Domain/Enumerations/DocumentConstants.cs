namespace CommunityOS.Documents.Domain.Enumerations;

/// <summary>
/// Canonical string values for a version source. The value is stored as text,
/// not an enum id.
/// </summary>
public static class DocumentSources
{
    public const string Member = "member";
    public const string System = "system";
    public const string Import = "import";
}

/// <summary>
/// Canonical string values for a document owner reference. Stored as text;
/// the owner is resolved through the owning service.
/// </summary>
public static class DocumentOwnerTypes
{
    public const string Person = "person";
    public const string OrganizationUnit = "organizationunit";
}