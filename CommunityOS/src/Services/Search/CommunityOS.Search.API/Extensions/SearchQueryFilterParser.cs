namespace CommunityOS.Search.API.Extensions;

public static class SearchQueryFilterParser
{
    public static string[]? Normalize(string?[]? values)
    {
        if (values is null || values.Length == 0) return null;
        var filters = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
        return filters.Length == 0 ? null : filters;
    }
}
