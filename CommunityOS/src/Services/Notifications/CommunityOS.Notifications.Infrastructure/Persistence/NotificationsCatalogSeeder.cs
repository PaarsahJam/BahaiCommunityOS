using CommunityOS.Notifications.Domain.Aggregates;
using CommunityOS.Notifications.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Notifications.Infrastructure.Persistence;

/// <summary>
/// Seeds the ratified baseline notification-type catalog (ADR-025) so a fresh
/// database can dispatch notifications immediately. The catalog is configuration
/// — never a hard enum — and remains extensible at runtime through the types API
/// (<c>notifications.type.manage</c>). The baseline codes are
/// <c>task-assigned, task-escalated, task-completed, task-cancelled,
/// record-verified, record-rejected, record-hold (sensitive),
/// question-flagged, community-activity, community-event, community-meeting,
/// general</c>. Seeding is idempotent and never overwrites or retires types that
/// already exist.
/// </summary>
public static class NotificationsCatalogSeeder
{
    /// <summary>
    /// Well-known actor recorded as <c>CreatedBy</c> for configuration-time
    /// baseline entries (never a real person id; not PII).
    /// </summary>
    public const string BaselineActor = "00000000-0000-0000-0000-000000000001";

    public static readonly Guid BaselineActorId = Guid.Parse(BaselineActor);

    /// <summary>The ratified baseline catalog: (code, display name, default channel, subject, body, sensitive).</summary>
    public static readonly IReadOnlyList<(
        string Code,
        string DisplayName,
        NotificationChannel DefaultChannel,
        string SubjectTemplate,
        string BodyTemplate,
        bool IsSensitive)> Baseline =
    [
        ("task-assigned", "Task assigned", NotificationChannel.InApp,
            "A task has been assigned to you", "Task {{TaskId}} has been assigned to you.", false),
        ("task-escalated", "Task escalated", NotificationChannel.InApp,
            "A task has been escalated to you", "Task {{TaskId}} has been escalated to you.", false),
        ("task-completed", "Task completed", NotificationChannel.InApp,
            "A task you initiated has been completed", "Task {{TaskId}} has been completed.", false),
        ("task-cancelled", "Task cancelled", NotificationChannel.InApp,
            "A task you initiated has been cancelled", "Task {{TaskId}} has been cancelled.", false),
        ("record-verified", "Record verified", NotificationChannel.InApp,
            "Your record submission has been verified", "Record {{RecordId}} has been verified.", false),
        ("record-rejected", "Record not verified", NotificationChannel.InApp,
            "Your record submission was not verified", "Record {{RecordId}} was not verified.", false),
        ("record-hold", "Record on hold", NotificationChannel.InApp,
            "A record you are involved with is on hold", "Record {{RecordId}} is on hold.", true),
        ("question-flagged", "Question flagged", NotificationChannel.InApp,
            "A question requires moderation", "Question {{QuestionId}} requires moderation.", false),
        ("community-activity", "Community activity", NotificationChannel.InApp,
            "New community activity", "There is new activity in your community.", false),
        ("community-event", "Community event", NotificationChannel.InApp,
            "New community event", "A community event has been scheduled.", false),
        ("community-meeting", "Community meeting", NotificationChannel.InApp,
            "New community meeting", "A community meeting has been scheduled.", false),
        ("general", "General notice", NotificationChannel.InApp,
            "Notice", "A notification requires your attention.", false)
    ];

    /// <summary>
    /// Inserts any missing baseline types. Existing rows (including retired
    /// ones) are left untouched.
    /// </summary>
    public static async Task SeedBaselineTypesAsync(
        NotificationsDbContext db, CancellationToken ct = default)
    {
        foreach (var (code, displayName, defaultChannel, subject, body, isSensitive) in Baseline)
        {
            if (await db.NotificationTypes.AnyAsync(t => t.Code == code, ct))
                continue;

            db.NotificationTypes.Add(
                NotificationType.Create(
                    code, displayName, defaultChannel, subject, body,
                    isSensitive, BaselineActorId, DateTime.UtcNow));
        }

        await db.SaveChangesAsync(ct);
    }
}