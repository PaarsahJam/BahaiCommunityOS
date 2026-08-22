using NpgsqlTypes;

namespace CommunityOS.Search.Domain;

public static class SearchSourceTypes
{
    public const string Record = "record";
    public const string Document = "document";
    public const string WorkflowTask = "workflow-task";
    public const string KnowledgeQuestion = "knowledge-question";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        { Record, Document, WorkflowTask, KnowledgeQuestion };
}

public sealed class SearchDocument
{
    private SearchDocument() { }
    public Guid Id { get; private set; }
    public string SourceType { get; private set; } = null!;
    public Guid SourceId { get; private set; }
    public Guid? OrganizationUnitId { get; private set; }
    public string Status { get; private set; } = null!;
    public bool IsSensitive { get; private set; }
    public string DisplayTitle { get; private set; } = null!;
    public string TypeCode { get; private set; } = null!;
    public DateTime IndexedOn { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public NpgsqlTsVector SearchVector { get; private set; } = null!;
    public List<AdditionalScope> AdditionalScopes { get; private set; } = [];

    public static SearchDocument Create(string sourceType, Guid sourceId, string displayTitle, string typeCode,
        string status, bool sensitive, Guid? organizationUnitId, DateTime occurredOn) => new()
    {
        Id = Guid.NewGuid(), SourceType = ValidateSourceType(sourceType), SourceId = sourceId,
        DisplayTitle = Safe(displayTitle, 500), TypeCode = Safe(typeCode, 100), Status = Safe(status, 50),
        IsSensitive = sensitive, OrganizationUnitId = organizationUnitId, IndexedOn = occurredOn,
        CreatedOn = occurredOn
    };

    public bool Apply(string? displayTitle, string? typeCode, string? status, bool? sensitive,
        Guid? organizationUnitId, bool organizationUnitPresent, DateTime occurredOn, bool isCreated = false)
    {
        if (occurredOn < IndexedOn) return false;
        if (displayTitle is not null) DisplayTitle = Safe(displayTitle, 500);
        if (typeCode is not null) TypeCode = Safe(typeCode, 100);
        if (status is not null) Status = Safe(status, 50);
        if (sensitive is not null) IsSensitive = sensitive.Value;
        if (organizationUnitPresent) OrganizationUnitId = organizationUnitId;
        if (isCreated) CreatedOn = occurredOn;
        IndexedOn = occurredOn;
        return true;
    }

    public static string ValidateSourceType(string value)
    {
        if (!SearchSourceTypes.All.Contains(value)) throw new ArgumentException("Unknown search source type.", nameof(value));
        return value;
    }
    private static string Safe(string value, int max) => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim()[..Math.Min(value.Trim().Length, max)];
}

public sealed class AdditionalScope
{
    private AdditionalScope() { }
    public Guid SearchDocumentId { get; private set; }
    public Guid OrganizationUnitId { get; private set; }
    public static AdditionalScope Create(Guid documentId, Guid organizationUnitId) => new() { SearchDocumentId = documentId, OrganizationUnitId = organizationUnitId };
}

public sealed class OrganizationUnitReference
{
    private OrganizationUnitReference() { }
    public Guid OrganizationUnitId { get; private set; }
    public Guid? ParentOrganizationUnitId { get; private set; }
    public DateTime LastUpdatedOn { get; private set; }
    public static OrganizationUnitReference Create(Guid id, Guid? parent, DateTime occurredOn) => new() { OrganizationUnitId = id, ParentOrganizationUnitId = parent, LastUpdatedOn = occurredOn };
    public bool Apply(Guid? parent, DateTime occurredOn)
    {
        if (occurredOn < LastUpdatedOn) return false;
        ParentOrganizationUnitId = parent; LastUpdatedOn = occurredOn; return true;
    }
}

public sealed class SearchIndexLog
{
    public string SourceType { get; set; } = null!;
    public DateTime LastEventOccurredOn { get; set; }
    public long IndexedCount { get; set; }
}
