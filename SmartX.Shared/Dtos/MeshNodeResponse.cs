namespace SmartX.Shared;

/// <summary>
/// Serializable mesh vertex for the topology snapshot API (gateway or ESP32).
/// </summary>
public sealed class MeshNodeResponse
{
    public string Id { get; set; } = string.Empty;

    public MeshNodeKind Kind { get; set; }

    public string MacAddress { get; set; } = string.Empty;

    public string LocationNodeId { get; set; } = string.Empty;

    public SensorCategory? Category { get; set; }

    /// <summary>False when the node is disconnected / cannot forward.</summary>
    public bool IsOnline { get; set; }

    public FreshnessState Freshness { get; set; }

    public HealthState Health { get; set; }

    /// <summary>Degree in the undirected mesh (incident wireless links).</summary>
    public int NeighbourCount { get; set; }
}
