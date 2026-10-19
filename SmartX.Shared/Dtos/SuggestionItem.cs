namespace SmartX.Shared;

/// <summary>
/// One proactive insight or suggested override for the command-stream dashboard.
/// </summary>
public sealed class SuggestionItem
{
    public string Id { get; set; } = string.Empty;

    /// <summary>suggested-action or automated-insight</summary>
    public string Kind { get; set; } = "automated-insight";

    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string? RelatedDeviceId { get; set; }

    public CommandKind? SuggestedCommand { get; set; }

    /// <summary>Relative score from the sequence frequency map (higher = stronger pattern).</summary>
    public int Score { get; set; }
}
