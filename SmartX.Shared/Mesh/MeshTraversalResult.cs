namespace SmartX.Shared;

/// <summary>
/// Outcome of a BFS or DFS walk over the IoT mesh (Final PoE Stage 5.1+).
/// Visit order proves which vertices were discovered from the root; parent links
/// support path reconstruction and down-node hop suggestions (Stage 5.2).
/// </summary>
public sealed class MeshTraversalResult
{
    public required string RootId { get; init; }

    public required MeshTraversalMode Mode { get; init; }

    /// <summary>Discovery order starting with the root.</summary>
    public required IReadOnlyList<string> VisitOrder { get; init; }

    /// <summary>
    /// For each discovered non-root vertex, the neighbour that discovered it.
    /// Reconstruct a root→node path by walking parents backward.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Parents { get; init; }

    public int VisitedCount => VisitOrder.Count;

    /// <summary>
    /// Rebuilds the unique discovery path from root to <paramref name="nodeId"/>
    /// (empty if that node was not visited).
    /// </summary>
    public IReadOnlyList<string> PathTo(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return Array.Empty<string>();
        }

        if (string.Equals(nodeId, RootId, StringComparison.OrdinalIgnoreCase))
        {
            return [RootId];
        }

        if (!Parents.ContainsKey(nodeId) &&
            !VisitOrder.Any(id => string.Equals(id, nodeId, StringComparison.OrdinalIgnoreCase)))
        {
            return Array.Empty<string>();
        }

        var path = new List<string>();
        var current = nodeId;
        var guard = 0;
        while (!string.Equals(current, RootId, StringComparison.OrdinalIgnoreCase))
        {
            path.Add(current);
            if (!Parents.TryGetValue(current, out var parent))
            {
                return Array.Empty<string>();
            }

            current = parent;
            if (++guard > VisitOrder.Count + 1)
            {
                return Array.Empty<string>();
            }
        }

        path.Add(RootId);
        path.Reverse();
        return path;
    }
}
