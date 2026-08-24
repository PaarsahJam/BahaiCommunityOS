using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Localization.Application.Permissions;
using CommunityOS.Localization.Domain;
using MediatR;
using Microsoft.Extensions.Options;

namespace CommunityOS.Localization.Application;

public sealed record BundleItem(string NamespaceName, string Key, string Value, string ResolvedFrom);

public sealed record ExportBundleDto(
    long Version,
    string ETag,
    string CultureRequested,
    IReadOnlyList<BundleItem> Items);

public sealed record ExportBundleQuery(
    Guid ActorId,
    Guid? NamespaceId,
    string Culture) : IRequest<ExportBundleDto>;

/// <summary>
/// Resolves an export bundle for one culture. Approved values are matched
/// along the BCP-47 fallback chain of the requested culture (e.g.
/// fa-IR → fa → default locale), with the default locale as final fallback
/// (ADR-029 decision 3). Fail-visible: keys with no resolvable value are
/// omitted entirely so clients render the key identifier itself — bundles
/// never contain placeholder translations. The bundle is version-stamped
/// with the singleton catalog version for deterministic ETags (decision 17).
/// </summary>
public sealed class ExportBundleHandler(
    ILocalizationReader reader,
    AuthorizationGuard guard,
    IOptions<LocalizationOptions> options)
    : IRequestHandler<ExportBundleQuery, ExportBundleDto>
{
    public async Task<ExportBundleDto> Handle(ExportBundleQuery request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, LocalizationPermissions.ResourceRead, ct: cancellationToken);

        var culture = Bcp47.Normalize(request.Culture);
        var version = await reader.GetCatalogVersionAsync(cancellationToken);
        var candidates = await reader.LoadExportCandidatesAsync(
            request.NamespaceId, [culture], options.Value.MaxExportKeys, cancellationToken);

        var items = BundleResolver.Resolve(
            candidates, culture, await ResolveDefaultCultureAsync(reader, cancellationToken));

        return new ExportBundleDto(
            version,
            BundleResolver.ETagFor(version),
            culture,
            items);
    }

    private static async Task<string> ResolveDefaultCultureAsync(ILocalizationReader reader, CancellationToken ct)
    {
        var @default = await reader.FindDefaultLocaleAsync(ct);
        return @default?.Code ?? "en";
    }
}

public static class BundleResolver
{
    public static IReadOnlyList<BundleItem> Resolve(
        IReadOnlyList<ExportCandidateRow> candidates,
        string requestedCulture,
        string defaultCulture)
    {
        var chain = BuildChain(requestedCulture, defaultCulture);

        var items = new List<BundleItem>(candidates.Count);
        foreach (var candidate in candidates)
        {
            foreach (var link in chain)
            {
                if (candidate.ApprovedValues.TryGetValue(link, out var value))
                {
                    items.Add(new BundleItem(candidate.NamespaceName, candidate.Key, value, link));
                    break;
                }
            }
        }

        return items;
    }

    /// <summary>The ordered candidate cultures for resolution: the requested
    /// code first, then its parent sub-tags (fa-IR → fa), then the default
    /// locale. Duplicates and case variants collapse.</summary>
    public static IReadOnlyList<string> BuildChain(string requestedCulture, string defaultCulture)
    {
        var chain = new List<string>(Bcp47.FallbackChain(requestedCulture));
        var normalizedDefault = Bcp47.Normalize(defaultCulture);
        if (!chain.Contains(normalizedDefault, StringComparer.Ordinal))
        {
            chain.Add(normalizedDefault);
        }

        return chain;
    }

    public static string ETagFor(long version) => $"\"loc-v{version}\"";
}
