namespace SmartX.Shared;

/// <summary>
/// Currently active unique disconnect/error keys from the gateway HashSet.
/// </summary>
public sealed class UniqueAlertsResponse
{
    public int Count { get; set; }

    public List<string> Keys { get; set; } = [];
}
