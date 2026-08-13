using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Knowledge.Domain.Enumerations;

/// <summary>
/// Review state of an AI-generated suggestion. A suggestion is never published
/// as an accepted answer until a human moderator moves it to <c>Accepted</c>.
/// </summary>
public sealed class AiSuggestionReviewState : Enumeration<int>
{
    public static readonly AiSuggestionReviewState Suggested = new(1, "suggested");
    public static readonly AiSuggestionReviewState Accepted = new(2, "accepted");
    public static readonly AiSuggestionReviewState Rejected = new(3, "rejected");

    private AiSuggestionReviewState(int id, string name) : base(id, name) { }

    public static IEnumerable<AiSuggestionReviewState> All =>
        [Suggested, Accepted, Rejected];

    public static AiSuggestionReviewState FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown AiSuggestionReviewState id: {id}");

    public static AiSuggestionReviewState FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown AiSuggestionReviewState name: {name}");
}