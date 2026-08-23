namespace CommunityOS.Correspondence.Domain.Exceptions;

/// <summary>Thrown when a letter id does not exist or the caller may not know
/// it exists (uniform 404 anti-enumeration surface).</summary>
public sealed class LetterNotFoundException : Exception
{
    public LetterNotFoundException()
        : base("The requested letter was not found.")
    {
    }
}

/// <summary>Illegal lifecycle transition, revision conflict or other state
/// conflict (409 surface).</summary>
public sealed class LetterConflictException : Exception
{
    public LetterConflictException(string message)
        : base(message)
    {
    }
}

public sealed class TemplateNotFoundException : Exception
{
    public TemplateNotFoundException()
        : base("The requested template was not found.")
    {
    }
}
