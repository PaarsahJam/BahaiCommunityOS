namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// A letter template in the Correspondence registry (ADR-028 decision 6).
/// Creating a letter from a template snapshots its skeleton into the new
/// draft; later template edits never mutate existing letters. Deactivation is
/// soft.
/// </summary>
public sealed class Template
{
    private Template()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Unique stable code.</summary>
    public string Code { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string SubjectTemplate { get; private set; } = null!;

    public string BodyTemplate { get; private set; } = null!;

    public string CategoryCode { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedOn { get; private set; }

    public DateTime UpdatedOn { get; private set; }

    public static Template Create(
        string code, string title, string subjectTemplate, string bodyTemplate,
        string categoryCode, Guid createdBy, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code.Trim(),
            Title = title.Trim(),
            SubjectTemplate = subjectTemplate,
            BodyTemplate = bodyTemplate,
            CategoryCode = categoryCode.Trim(),
            IsActive = true,
            CreatedBy = createdBy,
            CreatedOn = now,
            UpdatedOn = now
        };

    public void Update(
        string? title, string? subjectTemplate, string? bodyTemplate,
        string? categoryCode, bool? isActive, DateTime now)
    {
        Title = title?.Trim() ?? Title;
        SubjectTemplate = subjectTemplate ?? SubjectTemplate;
        BodyTemplate = bodyTemplate ?? BodyTemplate;
        CategoryCode = categoryCode?.Trim() ?? CategoryCode;
        if (isActive is { } active)
        {
            IsActive = active;
        }

        UpdatedOn = now;
    }
}
