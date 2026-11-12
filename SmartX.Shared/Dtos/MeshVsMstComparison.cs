namespace SmartX.Shared;

/// <summary>
/// Side-by-side full mesh vs Prim MST: shows how many redundant wireless links
/// the power-efficient backbone drops (Final PoE Stage 6.3).
/// </summary>
public sealed class MeshVsMstComparison
{
    public int FullMeshEdgeCount { get; set; }

    public double FullMeshTotalCost { get; set; }

    public int MstEdgeCount { get; set; }

    public double MstTotalCost { get; set; }

    /// <summary>Full mesh edges that are not in the MST (redundant for a spanning tree).</summary>
    public int EdgesSaved { get; set; }

    /// <summary><see cref="FullMeshTotalCost"/> − <see cref="MstTotalCost"/>.</summary>
    public double CostSaved { get; set; }

    /// <summary>Percent of full-mesh radio cost avoided by using the MST backbone.</summary>
    public double CostSavingPercent { get; set; }

    /// <summary>Links present in the full mesh but omitted from the MST.</summary>
    public List<MeshEdgeResponse> RedundantEdges { get; set; } = [];

    /// <summary>One-line operator summary for demos / README.</summary>
    public string Summary { get; set; } = string.Empty;
}
