namespace SmartX.Shared;

/// <summary>
/// Builds a minimum spanning tree of the IoT mesh for power-efficient routing
/// toward the central gateway (Final PoE Stage 6.1).
///
/// Algorithm choice — <b>Prim’s</b> (not Kruskal’s):
/// - The routing tree is rooted at the gateway; Prim grows outward from that root,
///   so parent hops toward the gateway fall out naturally.
/// - The graph is already an adjacency list; Prim scans incident edges per vertex,
///   which matches that representation better than Kruskal’s global edge sort + Union-Find.
/// - Fleet size is small (~gateway + 16 ESP32s); a clear O(V·E) Prim pass is fine for demos.
/// </summary>
public static class MeshMstBuilder
{
    /// <summary>
    /// Prim’s MST starting at <paramref name="rootId"/> (default: <see cref="MeshGraph.GatewayNodeId"/>).
    /// When <paramref name="onlineOnly"/> is true, offline vertices are excluded from the tree
    /// (live power-efficient backbone among forwarding nodes).
    /// </summary>
    public static MeshMstResult Build(
        MeshGraph graph,
        string? rootId = null,
        bool onlineOnly = false)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var root = string.IsNullOrWhiteSpace(rootId)
            ? MeshGraph.GatewayNodeId
            : rootId.Trim();

        if (!graph.TryGetNode(root, out var rootNode) || rootNode is null)
        {
            throw new KeyNotFoundException($"MST root '{root}' is not in the graph.");
        }

        if (onlineOnly && !rootNode.IsOnline)
        {
            throw new InvalidOperationException(
                $"MST root '{root}' is offline; cannot grow an online-only routing tree.");
        }

        var eligible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in graph.Nodes)
        {
            if (onlineOnly && !node.IsOnline)
            {
                continue;
            }

            eligible.Add(node.Id);
        }

        if (!eligible.Contains(root))
        {
            throw new InvalidOperationException($"MST root '{root}' is not eligible for the tree.");
        }

        // Prim key: cheapest known edge connecting a tree vertex to each outsider.
        var bestCost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var bestParent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bestEdge = new Dictionary<string, MeshEdge>(StringComparer.OrdinalIgnoreCase);
        var inTree = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in eligible)
        {
            bestCost[id] = double.PositiveInfinity;
        }

        bestCost[root] = 0;

        var mstEdges = new List<MeshEdge>();
        var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hopCosts = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double totalCost = 0;

        while (inTree.Count < eligible.Count)
        {
            string? next = null;
            var nextCost = double.PositiveInfinity;

            foreach (var id in eligible)
            {
                if (inTree.Contains(id))
                {
                    continue;
                }

                var cost = bestCost[id];
                if (next is null ||
                    cost < nextCost ||
                    (cost == nextCost &&
                     string.Compare(id, next, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    next = id;
                    nextCost = cost;
                }
            }

            if (next is null || double.IsPositiveInfinity(nextCost))
            {
                // Remaining eligible vertices are in another component — stop with a forest fragment.
                break;
            }

            inTree.Add(next);

            if (!string.Equals(next, root, StringComparison.OrdinalIgnoreCase) &&
                bestEdge.TryGetValue(next, out var treeEdge))
            {
                mstEdges.Add(treeEdge);
                parents[next] = bestParent[next];
                hopCosts[next] = treeEdge.Weight;
                totalCost += treeEdge.Weight;
            }

            foreach (var edge in graph.Neighbours(next))
            {
                var other = edge.OtherEnd(next);
                if (!eligible.Contains(other) || inTree.Contains(other))
                {
                    continue;
                }

                var improves = edge.Weight < bestCost[other];
                var tiesBreakTowardNext = edge.Weight == bestCost[other] &&
                    (!bestParent.TryGetValue(other, out var currentParent) ||
                     string.Compare(next, currentParent, StringComparison.OrdinalIgnoreCase) < 0);

                if (!improves && !tiesBreakTowardNext)
                {
                    continue;
                }

                bestCost[other] = edge.Weight;
                bestParent[other] = next;
                bestEdge[other] = edge;
            }
        }

        // Stable edge order for API / UI diffs against the full mesh.
        mstEdges.Sort(CompareEdges);

        return new MeshMstResult
        {
            Algorithm = "Prim",
            RootId = root,
            Edges = mstEdges,
            TotalCost = totalCost,
            Parents = parents,
            ParentHopCosts = hopCosts
        };
    }

    private static int CompareEdges(MeshEdge a, MeshEdge b)
    {
        var from = string.Compare(a.FromId, b.FromId, StringComparison.OrdinalIgnoreCase);
        if (from != 0)
        {
            return from;
        }

        var to = string.Compare(a.ToId, b.ToId, StringComparison.OrdinalIgnoreCase);
        if (to != 0)
        {
            return to;
        }

        return a.Weight.CompareTo(b.Weight);
    }
}
