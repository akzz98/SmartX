namespace SmartX.Shared;

/// <summary>
/// Undirected wireless link between two mesh vertices.
/// <see cref="Weight"/> is the routing cost (lower = preferred hop for MST / power-efficient paths).
/// </summary>
public sealed class MeshEdge
{
    public string FromId { get; init; } = string.Empty;

    public string ToId { get; init; } = string.Empty;

    /// <summary>Non-negative link cost (RSSI-derived or power/hop cost).</summary>
    public double Weight { get; init; }

    public MeshLinkKind LinkKind { get; init; } = MeshLinkKind.PowerCost;

    /// <summary>Optional operator-facing note (e.g. "rack1 ↔ rack2 ESP-NOW").</summary>
    public string? Label { get; init; }

    public string OtherEnd(string nodeId)
    {
        if (string.Equals(nodeId, FromId, StringComparison.OrdinalIgnoreCase))
        {
            return ToId;
        }

        if (string.Equals(nodeId, ToId, StringComparison.OrdinalIgnoreCase))
        {
            return FromId;
        }

        throw new ArgumentException($"Node '{nodeId}' is not an endpoint of this edge.", nameof(nodeId));
    }
}
