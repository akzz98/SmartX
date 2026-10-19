namespace SmartX.Shared;

/// <summary>
/// Timestamp-ordered command/alert history from the gateway SortedDictionary log.
/// </summary>
public sealed class TimelineHistoryResponse
{
    /// <summary>Oldest first — ready for timeline graphs without a client-side sort.</summary>
    public List<TimelineEntryResponse> Entries { get; set; } = [];

    public int TotalCount { get; set; }
}
