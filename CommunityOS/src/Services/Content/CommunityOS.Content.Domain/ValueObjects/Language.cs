using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Content.Domain.ValueObjects;

public sealed class Language : ValueObject
{
    public string Code { get; }
    public string DisplayName { get; }

    private Language(string code, string displayName)
    {
        Code = code;
        DisplayName = displayName;
    }

    public static Language Create(string code, string displayName)
    {
        Guard.NotNullOrWhiteSpace(code, nameof(code));
        Guard.NotNullOrWhiteSpace(displayName, nameof(displayName));
        if (code.Length < 2 || code.Length > 5)
            throw new ArgumentException("Language code must be BCP-47 format.", nameof(code));
        return new Language(code.ToLowerInvariant(), displayName.Trim());
    }

    public static Language English => Create("en", "English");
    public static Language Persian => Create("fa", "Persian");
    public static Language Arabic  => Create("ar", "Arabic");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }

    public override string ToString() => Code;
}
