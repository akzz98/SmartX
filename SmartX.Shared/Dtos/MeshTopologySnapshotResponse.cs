namespace SmartX.Shared;

/// <summary>
/// Full IoT mesh snapshot: vertices, undirected edges, and online/offline status.
/// Consumed by the topology dashboard (Final PoE Stage 4.3+).
/// </summary>
public sealed class MeshTopologySnapshotResponse
{
    public string GatewayNodeId { get; set; } = MeshGraph.GatewayNodeId;

    public int NodeCount { get; set; }

    public int EdgeCount { get; set; }

    public int OnlineNodeCount { get; set; }

    public int OfflineNodeCount { get; set; }

    public List<MeshNodeResponse> Nodes { get; set; } = [];

    public List<MeshEdgeResponse> Edges { get; set; } = [];
}
