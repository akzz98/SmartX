namespace SmartX.Shared;

/// <summary>
/// Fleet list payload for the filterable sensor view (Progressive Disclosure level 1).
/// TotalCount is the unpaged size so the overview can show how large the subset is.
/// </summary>
public sealed class SensorListResponse
{
    public List<SensorResponse> Sensors { get; set; } = [];

    public int TotalCount { get; set; }
}
