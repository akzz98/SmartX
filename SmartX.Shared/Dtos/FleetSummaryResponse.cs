namespace SmartX.Shared;

/// <summary>
/// Progressive Disclosure level 0: which devices need attention, rolled up by location and category.
/// Exception/stale counts stay 0 until health and freshness classification runs.
/// </summary>
public sealed class FleetSummaryResponse
{
    public int DeviceCount { get; set; }

    public int ExceptionCount { get; set; }

    public int StaleOrDisconnectedCount { get; set; }

    public Dictionary<string, int> ByCategory { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> ByLocation { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> ByHealth { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> ByFreshness { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Latest watts from every power meter, combined with operator +.</summary>
    public int CombinedSiteWatts { get; set; }

    /// <summary>Most recent site-level watt change using operator -.</summary>
    public int? LatestWattDelta { get; set; }
}
