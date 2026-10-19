namespace SmartX.Shared;

/// <summary>
/// Proactive recommendations from search/command pattern analysis.
/// </summary>
public sealed class SuggestionsResponse
{
    public List<SuggestionItem> Suggestions { get; set; } = [];

    public int SearchHistoryCount { get; set; }

    public int SequencePairCount { get; set; }
}
