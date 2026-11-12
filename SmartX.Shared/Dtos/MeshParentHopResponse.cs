namespace SmartX.Shared;

/// <summary>
/// One MST hop: child telemetry should forward to <see cref="ParentId"/> toward the gateway.
/// </summary>
public sealed class MeshParentHopResponse
{
    public string NodeId { get; set; } = string.Empty;

    public string ParentId { get; set; } = string.Empty;

    public double HopCost { get; set; }
}
