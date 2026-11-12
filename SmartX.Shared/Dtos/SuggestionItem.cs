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

    /// <summary>
    /// Normalised confidence in [0, 1] from pattern strength / evidence
    /// (not a raw occurrence count).
    /// </summary>
    public double Confidence { get; set; }
}
