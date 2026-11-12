namespace SmartX.Shared;

/// <summary>
/// AVL-backed timestamp range query for one sensor (Final PoE tree structures).
/// </summary>
public sealed class TelemetryRangeHistoryResponse
{
    public string DeviceId { get; set; } = string.Empty;

    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }

    public string? MetricFilter { get; set; }

    public int MatchCount { get; set; }

    /// <summary>Samples stored in this device's AVL tree (full history size).</summary>
    public int TreeSize { get; set; }

    /// <summary>
    /// AVL nodes examined during the range walk. Should stay near log(n)+k,
    /// not equal to TreeSize, when the window is narrow.
    /// </summary>
    public int NodesVisited { get; set; }

    public DateTimeOffset? TreeMinTimestamp { get; set; }

    public DateTimeOffset? TreeMaxTimestamp { get; set; }

    public List<TelemetryRangePointResponse> Points { get; set; } = [];
}

public sealed class TelemetryRangePointResponse
{
    public DateTimeOffset Timestamp { get; set; }

    public int Sequence { get; set; }

    public string Metric { get; set; } = string.Empty;

    public double? NumericValue { get; set; }

    public bool? BoolValue { get; set; }

    public string Summary { get; set; } = string.Empty;
}
