namespace CommunityOS.Records.Domain.Enumerations;

/// <summary>
/// Canonical string values for a record subject reference. Subject ids are
/// stable person/household/organization-unit ids resolved through the owning
/// services; names and PII are never stored locally (ADR-023).
/// </summary>
public static class RecordSubjectTypes
{
    public const string Person = "person";
    public const string Household = "household";
    public const string OrganizationUnit = "organizationunit";
    public const string Other = "other";

    public static bool IsValid(string? subjectType) =>
        subjectType is not null &&
        (string.Equals(subjectType, Person, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(subjectType, Household, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(subjectType, OrganizationUnit, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(subjectType, Other, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Canonical string values for a record evidence reference type.
/// </summary>
public static class RecordReferenceTypes
{
    public const string Evidence = "evidence";
    public const string Supporting = "supporting";
    public const string Certificate = "certificate";
}

/// <summary>
/// Canonical string values for a hold type.
/// </summary>
public static class RecordHoldTypes
{
    public const string Legal = "legal";
    public const string Administrative = "administrative";
}

/// <summary>
/// Canonical string values for a retention rule start trigger.
/// </summary>
public static class RetentionStartTriggers
{
    public const string RecordDate = "recordDate";
    public const string VerifiedDate = "verifiedDate";
}

/// <summary>
/// Canonical string values for a retention disposition. This prompt supports
/// <c>review</c> only — retention expiry never destroys data (ADR-023).
/// </summary>
public static class RetentionDispositions
{
    public const string Review = "review";
}
