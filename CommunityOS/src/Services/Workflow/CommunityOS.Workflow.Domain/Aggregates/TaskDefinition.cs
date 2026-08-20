using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Workflow.Domain.Aggregates;

/// <summary>
/// Aggregate root of the Workflow task-definition catalog (ADR-024): the task
/// types/templates the engine can instantiate. A stable string <c>Code</c>,
/// display name, description, the <c>DomainType</c> the task works over,
/// permitted outcome codes, deadline/SLA policy, and audit provenance.
/// Definitions are configuration: seeded idempotently, extensible at runtime,
/// never deleted (retired instead).
/// </summary>
public sealed class TaskDefinition : AggregateRoot<Guid>
{
    private readonly List<TaskDefinitionOutcome> _permittedOutcomes = [];

    private TaskDefinition() : base(Guid.Empty)
    {
        Code = null!;
        DisplayName = null!;
        Description = null!;
        DomainType = null!;
    }

    private TaskDefinition(
        Guid id,
        string code,
        string displayName,
        string description,
        string domainType,
        IReadOnlyList<string> permittedOutcomes,
        string? dueIn,
        bool requiresHumanReview,
        Guid createdBy,
        DateTime occurredOn) : base(id)
    {
        Code = code;
        DisplayName = displayName;
        Description = description;
        DomainType = domainType;
        _permittedOutcomes.AddRange(
            permittedOutcomes.Distinct().Select(o => new TaskDefinitionOutcome(Guid.NewGuid(), o)));
        DueIn = dueIn;
        RequiresHumanReview = requiresHumanReview;
        IsActive = true;
        CreatedBy = createdBy;
        CreatedOn = occurredOn.ToUniversalTime();
        UpdatedBy = createdBy;
        UpdatedOn = occurredOn.ToUniversalTime();
    }

    public string Code { get; private set; }

    public string DisplayName { get; private set; }

    public string Description { get; private set; }

    public string DomainType { get; private set; }

    public IReadOnlyList<TaskDefinitionOutcome> PermittedOutcomes => _permittedOutcomes.AsReadOnly();

    public IReadOnlyList<string> PermittedOutcomeCodes => _permittedOutcomes.Select(o => o.Code).ToArray();

    /// <summary>ISO-8601 duration, e.g. <c>P14D</c>. Null means no SLA policy.</summary>
    public string? DueIn { get; private set; }

    public bool RequiresHumanReview { get; private set; }

    public bool IsActive { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public Guid UpdatedBy { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public Guid? RetiredBy { get; private set; }

    public DateTime? RetiredOn { get; private set; }

    public bool IsRetired => !IsActive;

    public static TaskDefinition Create(
        string code,
        string displayName,
        string description,
        string domainType,
        IReadOnlyList<string> permittedOutcomes,
        string? dueIn,
        bool requiresHumanReview,
        Guid createdBy,
        DateTime occurredOn)
    {
        Guard.NotNullOrWhiteSpace(code, nameof(code));
        Guard.MaxLength(code, 100, nameof(code));
        Guard.NotNullOrWhiteSpace(displayName, nameof(displayName));
        Guard.MaxLength(displayName, 200, nameof(displayName));
        Guard.NotNullOrWhiteSpace(description, nameof(description));
        Guard.MaxLength(description, 500, nameof(description));
        Guard.NotNullOrWhiteSpace(domainType, nameof(domainType));
        Guard.MaxLength(domainType, 50, nameof(domainType));

        var outcomes = permittedOutcomes ?? [];
        if (outcomes.Count == 0)
            throw new ArgumentException("At least one permitted outcome is required.", nameof(permittedOutcomes));

        var definition = new TaskDefinition(
            Guid.NewGuid(),
            code.Trim(),
            displayName.Trim(),
            description.Trim(),
            domainType.Trim().ToLowerInvariant(),
            outcomes.Select(o => o.Trim().ToLowerInvariant()).ToArray(),
            dueIn,
            requiresHumanReview,
            createdBy,
            occurredOn);

        return definition;
    }

    public void Update(
        string displayName,
        string description,
        string domainType,
        IReadOnlyList<string> permittedOutcomes,
        string? dueIn,
        bool requiresHumanReview,
        Guid updatedBy,
        DateTime occurredOn)
    {
        if (IsRetired)
            throw new CommunityOS.Workflow.Domain.Exceptions.RetiredTaskDefinitionUpdateException(Code);

        Guard.NotNullOrWhiteSpace(displayName, nameof(displayName));
        Guard.MaxLength(displayName, 200, nameof(displayName));
        Guard.NotNullOrWhiteSpace(description, nameof(description));
        Guard.MaxLength(description, 500, nameof(description));
        Guard.NotNullOrWhiteSpace(domainType, nameof(domainType));
        Guard.MaxLength(domainType, 50, nameof(domainType));

        var outcomes = permittedOutcomes ?? [];
        if (outcomes.Count == 0)
            throw new ArgumentException("At least one permitted outcome is required.", nameof(permittedOutcomes));

        DisplayName = displayName.Trim();
        Description = description.Trim();
        DomainType = domainType.Trim().ToLowerInvariant();
        DueIn = dueIn;
        RequiresHumanReview = requiresHumanReview;
        _permittedOutcomes.Clear();
        _permittedOutcomes.AddRange(
            outcomes.Distinct().Select(o => new TaskDefinitionOutcome(Guid.NewGuid(), o.Trim().ToLowerInvariant())));
        UpdatedBy = updatedBy;
        UpdatedOn = occurredOn.ToUniversalTime();
    }

    /// <summary>
    /// Retires the definition (idempotent). Retiring is only permitted when no
    /// open tasks reference the definition; the caller checks
    /// <c>HasOpenTasksAsync</c> before invoking.
    /// </summary>
    public void Retire(Guid retiredBy, DateTime occurredOn)
    {
        if (IsRetired)
            return;

        IsActive = false;
        RetiredBy = retiredBy;
        RetiredOn = occurredOn.ToUniversalTime();
        UpdatedBy = retiredBy;
        UpdatedOn = occurredOn.ToUniversalTime();
    }

    public bool PermitsOutcome(string outcome) =>
        _permittedOutcomes.Any(o =>
            string.Equals(o.Code, outcome, StringComparison.OrdinalIgnoreCase));
}