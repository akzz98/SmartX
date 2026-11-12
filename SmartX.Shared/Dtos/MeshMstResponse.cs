namespace SmartX.Shared;

/// <summary>
/// Prim’s MST API payload: selected edges, total power cost, and parent hops toward the gateway
/// (Final PoE Stage 6.2).
/// </summary>
public sealed class MeshMstResponse
{
    public string Algorithm { get; set; } = "Prim";

    public string RootId { get; set; } = string.Empty;

    public bool OnlineOnly { get; set; }

    public int EdgeCount { get; set; }

    public int NodeCount { get; set; }

    public double TotalCost { get; set; }

    public List<MeshEdgeResponse> Edges { get; set; } = [];

    /// <summary>Per-node next hop toward the gateway (sorted by node id).</summary>
    public List<MeshParentHopResponse> ParentHops { get; set; } = [];

    /// <summary>Full mesh vs MST backbone comparison (edge/cost savings + redundant links).</summary>
    public MeshVsMstComparison Comparison { get; set; } = new();
}
