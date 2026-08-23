namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// Ratified fixed reason-code vocabularies (ADR-028). Free-text reasons are
/// never accepted — codes keep logs, events and history free of prohibited
/// content.
/// </summary>
public static class CorrespondenceReasonCodes
{
    public static readonly IReadOnlyList<string> Cancellation =
        ["draft-error", "superseded", "withdrawn-by-institution", "other"];

    public static readonly IReadOnlyList<string> DeliveryFailure =
        ["bad-address", "refused", "unclaimed", "returned-to-sender", "provider-error", "other"];

    public static readonly IReadOnlyList<string> HoldPlacement =
        ["investigation", "legal-request", "dispute", "regulatory-inquiry", "other"];

    public static bool IsKnown(IReadOnlyList<string> vocabulary, string code) =>
        vocabulary.Contains(code, StringComparer.Ordinal);
}
