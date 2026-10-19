namespace SmartX.Shared;

/// <summary>
/// One operator search/filter on the command stream. Pattern analysis uses these
/// sequences (e.g. moisture alert context → pump search) to score suggestions.
/// </summary>
public sealed class OperatorSearchEvent
{
    public string Id { get; set; } = string.Empty;

    public DateTimeOffset SearchedAt { get; set; }

    public string? CategoryFilter { get; set; }

    public string? HealthFilter { get; set; }

    public string? AlertScope { get; set; }

    public string? RelatedDeviceId { get; set; }

    /// <summary>Short label used as a sequence key in the frequency map (e.g. moisture-critical).</summary>
    public string PatternKey { get; set; } = string.Empty;
}
