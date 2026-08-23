using System.Globalization;
using System.Text.RegularExpressions;
using CommunityOS.Correspondence.Application;
using Microsoft.Extensions.Options;

namespace CommunityOS.Correspondence.Infrastructure.Retention;

/// <summary>
/// Options-driven retention policy (ADR-028 decision 13): resolves each
/// letter's category through EventClasses to a class, then maps the class to
/// an ISO 8601 duration applied with calendar arithmetic. Classes without a
/// duration (or absent from configuration) are indefinite — ExpiresOn stays
/// null and expiry alone never deletes anything.
/// </summary>
public sealed partial class CorrespondenceRetentionPolicy : IRetentionPolicy
{
    private readonly CorrespondenceOptions _options;

    public CorrespondenceRetentionPolicy(IOptions<CorrespondenceOptions> options)
    {
        _options = options.Value;
        // Fail fast on malformed durations at startup rather than at first
        // submission: validate every configured class once here.
        foreach (var (name, duration) in _options.Retention.Classes)
        {
            if (!string.IsNullOrWhiteSpace(duration))
            {
                Validate(name, duration);
            }
        }
    }

    public RetentionAssignment Assign(string categoryCode, DateTime submittedOn)
    {
        var @class = _options.Retention.EventClasses.TryGetValue(categoryCode, out var mapped)
            ? mapped
            : _options.Retention.DefaultClass;

        return new RetentionAssignment(@class, ExpiresOnFor(@class, submittedOn));
    }

    private DateTime? ExpiresOnFor(string @class, DateTime occurredOn)
    {
        var iso = _options.Retention.Classes.TryGetValue(@class, out var configured) &&
                  !string.IsNullOrWhiteSpace(configured)
            ? configured
            : null;
        return iso is null ? null : ApplyDuration(iso, occurredOn);
    }

    [GeneratedRegex(
        @"^P(?!$)(?:(?<years>\d+)Y)?(?:(?<months>\d+)M)?(?:(?<weeks>\d+)W)?(?:(?<days>\d+)D)?" +
        @"(?:T(?!$)(?:(?<hours>\d+)H)?(?:(?<minutes>\d+)M)?(?:(?<seconds>\d+(?:\.\d+)?)S)?)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();

    private static void Validate(string name, string isoDuration)
    {
        if (!DurationPattern().IsMatch(isoDuration))
        {
            throw new InvalidOperationException(
                $"Retention class '{name}' has an invalid ISO 8601 duration '{isoDuration}'.");
        }
    }

    private static DateTime ApplyDuration(string isoDuration, DateTime instant)
    {
        var match = DurationPattern().Match(isoDuration);
        int Group(string name) =>
            match.Groups[name].Success
                ? int.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture)
                : 0;

        return instant
            .AddYears(Group("years"))
            .AddMonths(Group("months"))
            .AddDays(7L * Group("weeks"))
            .AddDays(Group("days"))
            .AddHours(Group("hours"))
            .AddMinutes(Group("minutes"))
            .AddSeconds(match.Groups["seconds"].Success
                ? double.Parse(match.Groups["seconds"].Value, CultureInfo.InvariantCulture)
                : 0);
    }
}
