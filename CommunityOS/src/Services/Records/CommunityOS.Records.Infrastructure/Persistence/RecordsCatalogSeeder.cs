using CommunityOS.Records.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Records.Infrastructure.Persistence;

/// <summary>
/// Seeds the ratified baseline category catalog (ADR-023) so a fresh database
/// can create records immediately. The catalog is configuration — never a hard
/// enum — and remains open to the ratified Data Classification Model; the
/// baseline codes are <c>birth, marriage, death, membership, appointment,
/// official-community, administrative</c>. Additional categories are managed
/// through the API (<c>records.category.manage</c>). Seeding is idempotent and
/// never overwrites or retires categories that already exist.
/// </summary>
public static class RecordsCatalogSeeder
{
    /// <summary>
    /// Well-known actor recorded as <c>CreatedBy</c> for configuration-time
    /// baseline entries (never a real person id; not PII).
    /// </summary>
    public const string BaselineActor = "00000000-0000-0000-0000-000000000001";

    public static readonly Guid BaselineActorId = Guid.Parse(BaselineActor);

    /// <summary>The ratified baseline catalog: (code, display name, description).</summary>
    public static readonly IReadOnlyList<(string Code, string DisplayName, string Description)> Baseline =
    [
        ("birth", "Birth records", "Official birth records."),
        ("marriage", "Marriage records", "Official marriage records."),
        ("death", "Death records", "Official death records."),
        ("membership", "Membership records", "Enrollment and service records."),
        ("appointment", "Appointment records", "Official appointment records."),
        ("official-community", "Official community facts", "Official community facts and decisions."),
        ("administrative", "Administrative records", "Administrative and institutional records.")
    ];

    /// <summary>
    /// Inserts any missing baseline categories. Existing rows (including
    /// retired ones) are left untouched.
    /// </summary>
    public static async Task SeedBaselineCategoriesAsync(
        RecordsDbContext db, CancellationToken ct = default)
    {
        foreach (var (code, displayName, description) in Baseline)
        {
            if (await db.RecordCategories.AnyAsync(c => c.Code == code, ct))
                continue;

            db.RecordCategories.Add(
                RecordCategory.Create(code, displayName, description, BaselineActorId));
        }

        await db.SaveChangesAsync(ct);
    }
}