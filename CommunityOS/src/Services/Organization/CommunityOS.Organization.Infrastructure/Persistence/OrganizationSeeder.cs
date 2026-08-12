using CommunityOS.Organization.Domain.Aggregates;
using CommunityOS.Organization.Domain.Enumerations;
using CommunityOS.Organization.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CommunityOS.Organization.Infrastructure.Persistence;

using Organization = CommunityOS.Organization.Domain.Aggregates.Organization;

/// <summary>
/// Seeds a minimal development-only organization hierarchy (a national body, a
/// couple of units and a committee) so the API is usable in local development.
/// The seed is idempotent and only creates data when the organization table is
/// empty. It is a convenience, not a required runtime dependency, and is only
/// invoked from the development pipeline.
/// </summary>
public static class OrganizationSeeder
{
    public static async Task SeedDevelopmentDataAsync(
        OrganizationDbContext db, CancellationToken ct = default)
    {
        if (await db.Organizations.AnyAsync(ct))
            return;

        var now = DateTime.UtcNow;

        var national = Organization.Create(
            "National Spiritual Assembly of Persia",
            "NationalSpiritualAssembly",
            Jurisdiction.Global(),
            now.AddYears(-30));
        await db.Organizations.AddAsync(national, ct);

        var region = OrganizationUnit.Create(
            national.Id, "Regional Council — North", "RegionalCouncil", null,
            EffectivePeriod.Create(now));
        await db.OrganizationUnits.AddAsync(region, ct);

        var cluster = OrganizationUnit.Create(
            national.Id, "Cluster 1", "Cluster", region.Id,
            EffectivePeriod.Create(now));
        await db.OrganizationUnits.AddAsync(cluster, ct);

        var committee = Committee.Create(
            "Teaching Committee", "Teaching", national.Id, cluster.Id,
            Jurisdiction.Create(JurisdictionType.OrganizationUnit, cluster.Id));
        await db.Committees.AddAsync(committee, ct);

        var institution = Institution.Create(
            "Universal House of Justice", "WorldCentre", Jurisdiction.Global(), now.AddYears(-60));
        await db.Institutions.AddAsync(institution, ct);

        await db.SaveChangesAsync(ct);
    }
}