using CommunityOS.Localization.Domain.Exceptions;

namespace CommunityOS.Localization.Domain;

/// <summary>
/// A culture in the locale registry (ADR-029 decision 3). Registration alone
/// does not make a locale resolvable: only <see cref="Activate"/> does. The
/// default locale is activated by definition and can never be deactivated.
/// Seeding policy (ratified): implementations seed only <c>en</c> active;
/// further locales â€” including the ratified activation candidates <c>fa</c>
/// and <c>ar</c> â€” are activated by explicit admin command.
/// </summary>
public sealed class Locale
{
    private Locale()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Canonical BCP-47 code, unique and immutable after creation.</summary>
    public string Code { get; private set; } = null!;

    public string? DisplayName { get; private set; }

    public LocaleStatus Status { get; private set; }

    public bool IsDefault { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public static Locale Register(string code, string? displayName, Guid createdBy, DateTime now)
    {
        if (!Bcp47.IsValid(code))
        {
            throw new ArgumentException($"'{code}' is not a valid BCP-47 culture code.");
        }

        return new Locale
        {
            Id = Guid.NewGuid(),
            Code = Bcp47.Normalize(code),
            DisplayName = displayName?.Trim(),
            Status = LocaleStatus.Registered,
            IsDefault = false,
            CreatedBy = createdBy,
            CreatedOn = now,
            UpdatedOn = now
        };
    }

    /// <summary>Migration-seeded default locale (ratified seed policy:
    /// exactly one row â€” <c>en</c>, active). Not an application path.</summary>
    internal static Locale CreateDefaultSeed(
        Guid id, string code, string displayName, Guid createdBy, DateTime now) =>
        new()
        {
            Id = id,
            Code = code,
            DisplayName = displayName,
            Status = LocaleStatus.Active,
            IsDefault = true,
            CreatedBy = createdBy,
            CreatedOn = now,
            UpdatedOn = now
        };

    public void Activate(DateTime now)
    {
        if (Status == LocaleStatus.Active)
        {
            throw new LocalizationConflictException($"Locale '{Code}' is already active.");
        }

        Status = LocaleStatus.Active;
        UpdatedOn = now;
    }

    public void Deactivate(DateTime now)
    {
        if (IsDefault)
        {
            throw new LocalizationConflictException("The default locale cannot be deactivated.");
        }

        if (Status == LocaleStatus.Registered)
        {
            throw new LocalizationConflictException($"Locale '{Code}' is not active.");
        }

        Status = LocaleStatus.Registered;
        UpdatedOn = now;
    }

    /// <summary>Promotes this locale to the registry default; the caller is
    /// responsible for demoting the previous default in the same save.</summary>
    public void MakeDefault(DateTime now)
    {
        if (Status != LocaleStatus.Active)
        {
            // The default must be resolvable; promotion implies activation.
            Status = LocaleStatus.Active;
        }

        IsDefault = true;
        UpdatedOn = now;
    }

    public void ClearDefault(DateTime now)
    {
        IsDefault = false;
        UpdatedOn = now;
    }
}
