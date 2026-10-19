namespace SmartX.Shared;

/// <summary>
/// One SortedDictionary timeline row for the command-stream history / graph UI.
/// </summary>
public sealed class TimelineEntryResponse
{
    public string Kind { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string MacAddress { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public CommandResponse? Command { get; set; }

    public AlertResponse? Alert { get; set; }
}
