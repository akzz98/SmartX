namespace SmartX.Shared;

/// <summary>
/// Power-efficient routing tree produced by Prim’s algorithm on the IoT mesh
/// (Final PoE Stage 6.1). Parent links point toward the gateway root.
/// </summary>
public sealed class MeshMstResult
{
    /// <summary>Always <c>Prim</c> for this implementation — see <see cref="MeshMstBuilder"/>.</summary>
    public string Algorithm { get; init; } = "Prim";

    public required string RootId { get; init; }

    /// <summary>Undirected MST edges selected from the full mesh (n − 1 for a connected graph).</summary>
    public required IReadOnlyList<MeshEdge> Edges { get; init; }

    /// <summary>Sum of selected edge weights (total radio/power cost of the backbone).</summary>
    public required double TotalCost { get; init; }

    /// <summary>
    /// For each non-root vertex in the tree: neighbour closer to the gateway.
    /// Telemetry should hop <c>node → Parents[node] → … → root</c>.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Parents { get; init; }

    /// <summary>Weight of the MST edge from each child to its parent.</summary>
    public required IReadOnlyDictionary<string, double> ParentHopCosts { get; init; }

    public int EdgeCount => Edges.Count;

    public int NodeCount => Parents.Count + 1;
}
