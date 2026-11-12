namespace SmartX.Shared;

/// <summary>
/// Classifies mesh vertices as reachable / down / cut-off and suggests alternate
/// hops around faults (Final PoE Stage 5.2). Uses BFS so hop distance is meaningful.
/// </summary>
public static class MeshReachabilityAnalyzer
{
    public static MeshReachabilityReport Analyze(MeshGraph graph, string? rootId = null)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var root = string.IsNullOrWhiteSpace(rootId)
            ? MeshGraph.GatewayNodeId
            : rootId.Trim();

        // Topology walk (ignore online flags) → ideal paths.
        var topology = MeshGraphTraverser.BreadthFirst(graph, root, onlineOnly: false);

        // Live routing walk → only expand through online vertices.
        var live = MeshGraphTraverser.BreadthFirst(graph, root, onlineOnly: true);

        var reachableSet = new HashSet<string>(live.VisitOrder, StringComparer.OrdinalIgnoreCase);
        var down = new List<string>();
        var cutOff = new List<string>();
        var unreachable = new List<string>();

        foreach (var node in graph.Nodes.OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(node.Id, root, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!node.IsOnline)
            {
                down.Add(node.Id);
                unreachable.Add(node.Id);
                continue;
            }

            if (!reachableSet.Contains(node.Id))
            {
                cutOff.Add(node.Id);
                unreachable.Add(node.Id);
            }
        }

        var suggestions = new List<MeshHopSuggestion>(down.Count);
        foreach (var downId in down)
        {
            suggestions.Add(BuildHopSuggestion(graph, downId, topology, live));
        }

        return new MeshReachabilityReport
        {
            RootId = live.RootId,
            Mode = MeshTraversalMode.BreadthFirst,
            VisitOrder = live.VisitOrder,
            ReachableOnlineIds = live.VisitOrder
                .Where(id => !string.Equals(id, live.RootId, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            DownNodeIds = down,
            CutOffOnlineIds = cutOff,
            UnreachableIds = unreachable,
            DownNodeSuggestions = suggestions
        };
    }

    private static MeshHopSuggestion BuildHopSuggestion(
        MeshGraph graph,
        string downNodeId,
        MeshTraversalResult topology,
        MeshTraversalResult live)
    {
        var onlineNeighbours = new List<string>();
        foreach (var edge in graph.Neighbours(downNodeId))
        {
            var other = edge.OtherEnd(downNodeId);
            if (graph.TryGetNode(other, out var neighbour) &&
                neighbour is not null &&
                neighbour.IsOnline)
            {
                onlineNeighbours.Add(other);
            }
        }

        onlineNeighbours.Sort(StringComparer.OrdinalIgnoreCase);

        string? suggested = null;
        IReadOnlyList<string> alternatePath = Array.Empty<string>();
        var bestHops = int.MaxValue;

        foreach (var neighbourId in onlineNeighbours)
        {
            var path = live.PathTo(neighbourId);
            if (path.Count == 0)
            {
                continue;
            }

            // Prefer the live neighbour closest to the gateway (fewest hops).
            var hops = path.Count - 1;
            if (hops < bestHops)
            {
                bestHops = hops;
                suggested = neighbourId;
                alternatePath = path;
            }
        }

        return new MeshHopSuggestion
        {
            DownNodeId = downNodeId,
            IdealPathFromRoot = topology.PathTo(downNodeId),
            OnlineNeighbourIds = onlineNeighbours,
            SuggestedNextHop = suggested,
            AlternatePathToNextHop = alternatePath
        };
    }
}
