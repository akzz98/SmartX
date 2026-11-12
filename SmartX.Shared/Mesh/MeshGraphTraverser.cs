namespace SmartX.Shared;

/// <summary>
/// BFS and DFS over an undirected <see cref="MeshGraph"/>.
/// Used to discover which ESP32s are connected to the gateway subnet
/// (Final PoE Stage 5.1). Reachable/down reporting lands in Stage 5.2.
/// </summary>
public static class MeshGraphTraverser
{
    /// <summary>
    /// Breadth-first walk from <paramref name="rootId"/> (default: central gateway).
    /// Neighbours are expanded in stable id order so demos are deterministic.
    /// </summary>
    public static MeshTraversalResult BreadthFirst(MeshGraph graph, string? rootId = null)
        => Traverse(graph, rootId, MeshTraversalMode.BreadthFirst);

    /// <summary>
    /// Depth-first walk from <paramref name="rootId"/> (default: central gateway).
    /// Uses an explicit stack (iterative) so deep meshes do not blow the call stack.
    /// </summary>
    public static MeshTraversalResult DepthFirst(MeshGraph graph, string? rootId = null)
        => Traverse(graph, rootId, MeshTraversalMode.DepthFirst);

    private static MeshTraversalResult Traverse(
        MeshGraph graph,
        string? rootId,
        MeshTraversalMode mode)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var root = string.IsNullOrWhiteSpace(rootId)
            ? MeshGraph.GatewayNodeId
            : rootId.Trim();

        if (!graph.TryGetNode(root, out _))
        {
            throw new KeyNotFoundException($"Mesh root '{root}' is not in the graph.");
        }

        var visitOrder = new List<string>();
        var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (mode == MeshTraversalMode.BreadthFirst)
        {
            RunBfs(graph, root, visitOrder, parents, visited);
        }
        else
        {
            RunDfs(graph, root, visitOrder, parents, visited);
        }

        return new MeshTraversalResult
        {
            RootId = root,
            Mode = mode,
            VisitOrder = visitOrder,
            Parents = parents
        };
    }

    private static void RunBfs(
        MeshGraph graph,
        string root,
        List<string> visitOrder,
        Dictionary<string, string> parents,
        HashSet<string> visited)
    {
        var queue = new Queue<string>();
        visited.Add(root);
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            visitOrder.Add(current);

            foreach (var neighbour in OrderedNeighbours(graph, current))
            {
                if (!visited.Add(neighbour))
                {
                    continue;
                }

                parents[neighbour] = current;
                queue.Enqueue(neighbour);
            }
        }
    }

    private static void RunDfs(
        MeshGraph graph,
        string root,
        List<string> visitOrder,
        Dictionary<string, string> parents,
        HashSet<string> visited)
    {
        // Stack holds (node, parent). Parent is null for the root.
        var stack = new Stack<(string NodeId, string? ParentId)>();
        stack.Push((root, null));

        while (stack.Count > 0)
        {
            var (current, parent) = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (parent is not null)
            {
                parents[current] = parent;
            }

            visitOrder.Add(current);

            // Push reverse of ordered neighbours so the lowest id is popped first
            // (matches a natural left-to-right recursive DFS).
            var neighbours = OrderedNeighbours(graph, current);
            for (var i = neighbours.Count - 1; i >= 0; i--)
            {
                var neighbour = neighbours[i];
                if (!visited.Contains(neighbour))
                {
                    stack.Push((neighbour, current));
                }
            }
        }
    }

    /// <summary>
    /// Unique neighbour ids for a vertex, sorted for stable demos.
    /// </summary>
    private static List<string> OrderedNeighbours(MeshGraph graph, string nodeId)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in graph.Neighbours(nodeId))
        {
            ids.Add(edge.OtherEnd(nodeId));
        }

        var list = ids.ToList();
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }
}
