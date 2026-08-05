namespace CommunityOS.SharedKernel.Guards;

public static class Guard
{
    public static T NotNull<T>(T? value, string paramName) where T : class =>
        value ?? throw new ArgumentNullException(paramName);

    public static string NotNullOrWhiteSpace(string? value, string paramName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be null or whitespace.", paramName)
            : value;

    public static T NotDefault<T>(T value, string paramName) where T : struct =>
        EqualityComparer<T>.Default.Equals(value, default)
            ? throw new ArgumentException("Value cannot be the default.", paramName)
            : value;

    public static int PositiveOrZero(int value, string paramName) =>
        value < 0
            ? throw new ArgumentOutOfRangeException(paramName, "Value must be >= 0.")
            : value;

    public static int Positive(int value, string paramName) =>
        value <= 0
            ? throw new ArgumentOutOfRangeException(paramName, "Value must be > 0.")
            : value;

    public static string MaxLength(string value, int maxLength, string paramName) =>
        value.Length > maxLength
            ? throw new ArgumentException($"Value exceeds maximum length of {maxLength}.", paramName)
            : value;
}
