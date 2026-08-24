namespace CommunityOS.Localization.Application;

/// <summary>
/// Seam for machine-translation providers (ADR-029 decision 19). The default
/// implementation ships disabled: Localization is fully functional without a
/// provider, and suggestions only ever enter the ordinary human review
/// workflow — this seam can never publish. No provider endpoint, API key or
/// network call exists in the ratified gate; wiring a real provider is a
/// future infrastructure change behind this interface.
/// </summary>
public interface IMachineTranslationSuggestionSource
{
    bool IsEnabled { get; }

    /// <summary>Produces draft suggestion content for review. Never called by
    /// any handler while <see cref="IsEnabled"/> is false.</summary>
    Task<IReadOnlyList<SuggestionDraft>> DraftAsync(
        string sourceCulture, string targetCulture, IReadOnlyList<string> values, CancellationToken ct);
}

public sealed record SuggestionDraft(string Value, string ProviderCode);

public sealed class DisabledMachineTranslationSuggestionSource
    : IMachineTranslationSuggestionSource
{
    public bool IsEnabled => false;

    public Task<IReadOnlyList<SuggestionDraft>> DraftAsync(
        string sourceCulture, string targetCulture, IReadOnlyList<string> values, CancellationToken ct) =>
        throw new InvalidOperationException(
            "The machine-translation provider is disabled (ADR-029 decision 19).");
}
