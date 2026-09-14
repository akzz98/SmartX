namespace SmartX.Shared;

/// <summary>
/// Registered IoT device identity: hardware address, deployment placement, and category.
/// LastSeenAt is what freshness logic will use; it is not a UI-detected value.
/// </summary>
public sealed class SensorDevice
{
    public string Id { get; set; } = string.Empty;

    public string MacAddress { get; set; } = string.Empty;

    public string LocationNodeId { get; set; } = string.Empty;

    public SensorCategory Category { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }
}
