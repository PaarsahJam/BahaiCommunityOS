using CommunityOS.Workflow.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Workflow.Infrastructure.Persistence;

/// <summary>
/// Seeds the ratified baseline task-definition catalog (ADR-024) so a fresh
/// database can create tasks immediately. The catalog is configuration — never
/// a hard enum — and remains extensible at runtime through the definitions API
/// (<c>workflow.definition.manage</c>). The baseline codes are
/// <c>record-review, document-review, knowledge-moderation,
/// knowledge-ai-review, general</c>. Seeding is idempotent and never overwrites
/// or retires definitions that already exist.
/// </summary>
public static class WorkflowCatalogSeeder
{
    /// <summary>
    /// Well-known actor recorded as <c>CreatedBy</c> for configuration-time
    /// baseline entries (never a real person id; not PII).
    /// </summary>
    public const string BaselineActor = "00000000-0000-0000-0000-000000000001";

    public static readonly Guid BaselineActorId = Guid.Parse(BaselineActor);

    /// <summary>The ratified baseline catalog: (code, display name, description, domain type, outcomes, due-in).</summary>
    public static readonly IReadOnlyList<(
        string Code,
        string DisplayName,
        string Description,
        string DomainType,
        IReadOnlyList<string> PermittedOutcomes,
        string DueIn)> Baseline =
    [
        ("record-review", "Record review", "Human review of a record submission.", "record",
            ["verified", "rejected"], "P14D"),
        ("document-review", "Document review", "Human review of a document artifact.", "document",
            ["approved", "rejected", "clarification-requested"], "P14D"),
        ("knowledge-moderation", "Knowledge moderation", "Human moderation of a knowledge question.", "knowledge-question",
            ["merged", "archived"], "P14D"),
        ("knowledge-ai-review", "Knowledge AI review", "Human review of an AI suggestion.", "knowledge-ai-suggestion",
            ["accepted", "rejected"], "P14D"),
        ("general", "General work", "Free-standing work item without a domain entity.", "general",
            ["completed"], "P14D")
    ];

    /// <summary>
    /// Inserts any missing baseline definitions. Existing rows (including
    /// retired ones) are left untouched.
    /// </summary>
    public static async Task SeedBaselineDefinitionsAsync(
        WorkflowDbContext db, CancellationToken ct = default)
    {
        foreach (var (code, displayName, description, domainType, outcomes, dueIn) in Baseline)
        {
            if (await db.TaskDefinitions.AnyAsync(d => d.Code == code, ct))
                continue;

            db.TaskDefinitions.Add(
                TaskDefinition.Create(
                    code, displayName, description, domainType, outcomes, dueIn,
                    requiresHumanReview: true, BaselineActorId, DateTime.UtcNow));
        }

        await db.SaveChangesAsync(ct);
    }
}