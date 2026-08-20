using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Workflow.Domain.Aggregates;

/// <summary>
/// An append-only history entry on a workflow task (ADR-024): actor, action,
/// timestamp and optional outcome/note. Activity is stored for audit but is
/// <b>never</b> exported onto the integration bus. Notes on activity entries are
/// sensitive and excluded from activity DTOs.
/// </summary>
public sealed class TaskActivity : Entity<Guid>
{
    private TaskActivity() : base(Guid.Empty)
    {
        Action = null!;
    }

    internal TaskActivity(
        Guid id,
        string action,
        Guid actorId,
        DateTime occurredOn,
        string? outcome = null,
        string? notes = null) : base(id)
    {
        Action = action;
        ActorId = actorId;
        OccurredOn = occurredOn.ToUniversalTime();
        Outcome = outcome;
        Notes = notes;
    }

    public string Action { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTime OccurredOn { get; private set; }

    public string? Outcome { get; private set; }

    /// <summary>Sensitive. Excluded from activity DTOs and never exported.</summary>
    public string? Notes { get; private set; }
}