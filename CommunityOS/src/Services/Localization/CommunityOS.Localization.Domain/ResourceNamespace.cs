using CommunityOS.Localization.Domain.Exceptions;

namespace CommunityOS.Localization.Domain;

/// <summary>
/// A namespace in the resource catalog (ADR-029 decisions 4 and 18).
/// Names are unique, lower-cased dotted identifiers. The reserved
/// <c>library.</c> prefix is rejected at creation to enforce the ratified
/// Knowledge/Library boundary: no authoritative Writings content may ever
/// enter the Localization catalog.
/// </summary>
public sealed class ResourceNamespace
{
    /// <summary>Reserved prefixes no namespace may use (ADR-029 decision 18).</summary>
    public static readonly IReadOnlyList<string> ReservedPrefixes = ["library.", "knowledge."];

    private ResourceNamespace()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public static ResourceNamespace Create(string name, string? description, Guid createdBy, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A namespace name is required.");
        }

        var normalized = name.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 100 ||
            !normalized.All(c => char.IsAsciiLetterOrDigit(c) || c == '.'))
        {
            throw new ArgumentException(
                "Namespace names may contain ASCII letters, digits and dots only.");
        }

        foreach (var reserved in ReservedPrefixes)
        {
            if (normalized.StartsWith(reserved, StringComparison.Ordinal))
            {
                throw new LocalizationConflictException(
                    $"The '{reserved}' namespace prefix is reserved by the Knowledge/Library boundary (ADR-029 decision 18).");
            }
        }

        return new ResourceNamespace
        {
            Id = Guid.NewGuid(),
            Name = normalized,
            Description = description?.Trim(),
            CreatedBy = createdBy,
            CreatedOn = now,
            UpdatedOn = now
        };
    }

    public void UpdateDescription(string? description, DateTime now)
    {
        Description = description?.Trim();
        UpdatedOn = now;
    }

    /// <summary>Entity-translation source contexts obey the same reserved list.</summary>
    public static void EnsureSourceContextAllowed(string sourceContext)
    {
        var normalized = sourceContext.Trim().ToLowerInvariant();
        foreach (var reserved in ReservedPrefixes)
        {
            if (normalized.StartsWith(reserved.TrimEnd('.'), StringComparison.Ordinal))
            {
                throw new LocalizationConflictException(
                    "The Knowledge/Library boundary forbids entity translations for this source context (ADR-029 decision 18).");
            }
        }
    }
}
