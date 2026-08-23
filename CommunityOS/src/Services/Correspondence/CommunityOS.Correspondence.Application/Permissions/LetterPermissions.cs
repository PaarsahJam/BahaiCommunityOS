namespace CommunityOS.Correspondence.Application.Permissions;

/// <summary>
/// Correspondence capabilities (correspondence.*, ratified ADR-028 decision
/// 15). Exact ordinal membership only; no capability implies another —
/// correspondence.letter.admin never implies read, and hold placement still
/// requires read-level visibility (including the sensitive second pass) of
/// every target letter.
/// </summary>
public static class LetterPermissions
{
    /// <summary>Resource type used in authorization contexts for letters.</summary>
    public const string ResourceType = "correspondence.letter";

    public const string LetterRead = "correspondence.letter.read";
    public const string LetterReadSensitive = "correspondence.letter.read.sensitive";
    public const string LetterCreate = "correspondence.letter.create";
    public const string LetterUpdate = "correspondence.letter.update";
    public const string LetterSubmit = "correspondence.letter.submit";
    public const string LetterCancel = "correspondence.letter.cancel";
    public const string LetterExport = "correspondence.letter.export";
    public const string LetterAdmin = "correspondence.letter.admin";
    public const string TemplateRead = "correspondence.template.read";
    public const string TemplateManage = "correspondence.template.manage";

    /// <summary>All ten ratified capabilities.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        LetterRead, LetterReadSensitive, LetterCreate, LetterUpdate,
        LetterSubmit, LetterCancel, LetterExport, LetterAdmin,
        TemplateRead, TemplateManage
    ];
}
