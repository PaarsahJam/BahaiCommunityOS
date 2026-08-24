namespace CommunityOS.Localization.Domain.Exceptions;

/// <summary>Thrown when a referenced localization object does not exist or the
/// caller may not know it exists (uniform 404 anti-enumeration surface).</summary>
public sealed class LocalizationNotFoundException : Exception
{
    public LocalizationNotFoundException()
        : base("The requested item was not found.")
    {
    }

    public LocalizationNotFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>Illegal lifecycle transition, duplicate natural key, reserved-name
/// violation or other state conflict (409 surface).</summary>
public sealed class LocalizationConflictException : Exception
{
    public LocalizationConflictException(string message)
        : base(message)
    {
    }
}
