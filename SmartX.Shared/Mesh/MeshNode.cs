namespace SmartX.Shared;

/// <summary>
/// Vertex in the Smart-X mesh: the central gateway or an ESP32 sensor.
/// Online/offline mirrors gateway freshness so routing can skip down nodes.
/// </summary>
public sealed class MeshNode
{
    public string Id { get; init; } = string.Empty;

    public MeshNodeKind Kind { get; init; }

    /// <summary>MAC for sensors; empty for the logical gateway vertex.</summary>
    public string MacAddress { get; set; } = string.Empty;

    public string LocationNodeId { get; set; } = string.Empty;

    public SensorCategory? Category { get; set; }

    /// <summary>False when the node cannot forward or originate telemetry (down / disconnected).</summary>
    public bool IsOnline { get; set; } = true;

    public FreshnessState Freshness { get; set; } = FreshnessState.Live;

    public HealthState Health { get; set; } = HealthState.Normal;
}
