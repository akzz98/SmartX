namespace SmartX.Shared;

/// <summary>
/// Maps in-memory <see cref="MeshGraph"/> state to API DTOs.
/// </summary>
public static class MeshDtoMapper
{
    public static MeshTopologySnapshotResponse ToTopologySnapshot(MeshGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var nodes = graph.Nodes
            .OrderBy(n => n.Kind)
            .ThenBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .Select(n => ToNodeResponse(n, graph.Neighbours(n.Id).Count))
            .ToList();

        var edges = graph.Edges
            .Select(ToEdgeResponse)
            .ToList();

        var online = nodes.Count(n => n.IsOnline);

        return new MeshTopologySnapshotResponse
        {
            GatewayNodeId = MeshGraph.GatewayNodeId,
            NodeCount = nodes.Count,
            EdgeCount = edges.Count,
            OnlineNodeCount = online,
            OfflineNodeCount = nodes.Count - online,
            Nodes = nodes,
            Edges = edges
        };
    }

    public static MeshNodeResponse ToNodeResponse(MeshNode node, int neighbourCount)
        => new()
        {
            Id = node.Id,
            Kind = node.Kind,
            MacAddress = node.MacAddress,
            LocationNodeId = node.LocationNodeId,
            Category = node.Category,
            IsOnline = node.IsOnline,
            Freshness = node.Freshness,
            Health = node.Health,
            NeighbourCount = neighbourCount
        };

    public static MeshEdgeResponse ToEdgeResponse(MeshEdge edge)
        => new()
        {
            FromId = edge.FromId,
            ToId = edge.ToId,
            Weight = edge.Weight,
            LinkKind = edge.LinkKind,
            Label = edge.Label
        };

    public static MeshTraversalResponse ToTraversalResponse(MeshReachabilityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new MeshTraversalResponse
        {
            RootId = report.RootId,
            Mode = report.Mode,
            VisitOrder = report.VisitOrder.ToList(),
            ReachableOnlineIds = report.ReachableOnlineIds.ToList(),
            DownNodeIds = report.DownNodeIds.ToList(),
            CutOffOnlineIds = report.CutOffOnlineIds.ToList(),
            UnreachableIds = report.UnreachableIds.ToList(),
            DownNodeSuggestions = report.DownNodeSuggestions
                .Select(ToHopSuggestionResponse)
                .ToList()
        };
    }

    public static MeshHopSuggestionResponse ToHopSuggestionResponse(MeshHopSuggestion suggestion)
        => new()
        {
            DownNodeId = suggestion.DownNodeId,
            IdealPathFromRoot = suggestion.IdealPathFromRoot.ToList(),
            OnlineNeighbourIds = suggestion.OnlineNeighbourIds.ToList(),
            SuggestedNextHop = suggestion.SuggestedNextHop,
            AlternatePathToNextHop = suggestion.AlternatePathToNextHop.ToList()
        };

    public static MeshMstResponse ToMstResponse(MeshGraph graph, MeshMstResult mst, bool onlineOnly)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(mst);

        var parentHops = mst.Parents
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => new MeshParentHopResponse
            {
                NodeId = p.Key,
                ParentId = p.Value,
                HopCost = mst.ParentHopCosts.TryGetValue(p.Key, out var cost) ? cost : 0
            })
            .ToList();

        return new MeshMstResponse
        {
            Algorithm = mst.Algorithm,
            RootId = mst.RootId,
            OnlineOnly = onlineOnly,
            EdgeCount = mst.EdgeCount,
            NodeCount = mst.NodeCount,
            TotalCost = mst.TotalCost,
            Edges = mst.Edges.Select(ToEdgeResponse).ToList(),
            ParentHops = parentHops,
            Comparison = BuildMeshVsMstComparison(graph, mst, onlineOnly)
        };
    }

    public static MeshVsMstComparison BuildMeshVsMstComparison(
        MeshGraph graph,
        MeshMstResult mst,
        bool onlineOnly)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(mst);

        var meshEdges = onlineOnly
            ? graph.Edges.Where(e => BothEndsOnline(graph, e)).ToList()
            : graph.Edges.ToList();

        var mstKeys = new HashSet<string>(
            mst.Edges.Select(EdgeKey),
            StringComparer.OrdinalIgnoreCase);

        var redundant = meshEdges
            .Where(e => !mstKeys.Contains(EdgeKey(e)))
            .OrderBy(e => e.FromId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.ToId, StringComparer.OrdinalIgnoreCase)
            .Select(ToEdgeResponse)
            .ToList();

        var fullCost = meshEdges.Sum(e => e.Weight);
        var costSaved = fullCost - mst.TotalCost;
        var savingPct = fullCost <= 0
            ? 0
            : Math.Round(100.0 * costSaved / fullCost, 1);

        return new MeshVsMstComparison
        {
            FullMeshEdgeCount = meshEdges.Count,
            FullMeshTotalCost = fullCost,
            MstEdgeCount = mst.EdgeCount,
            MstTotalCost = mst.TotalCost,
            EdgesSaved = redundant.Count,
            CostSaved = Math.Round(costSaved, 4),
            CostSavingPercent = savingPct,
            RedundantEdges = redundant,
            Summary =
                $"Full mesh {meshEdges.Count} links (cost {fullCost:0.##}) → " +
                $"Prim MST {mst.EdgeCount} links (cost {mst.TotalCost:0.##}); " +
                $"drop {redundant.Count} redundant hops, save {savingPct:0.#}% radio cost."
        };
    }

    private static bool BothEndsOnline(MeshGraph graph, MeshEdge edge)
        => graph.TryGetNode(edge.FromId, out var from) && from is { IsOnline: true } &&
           graph.TryGetNode(edge.ToId, out var to) && to is { IsOnline: true };

    private static string EdgeKey(MeshEdge edge)
    {
        var a = edge.FromId;
        var b = edge.ToId;
        if (string.Compare(a, b, StringComparison.OrdinalIgnoreCase) > 0)
        {
            (a, b) = (b, a);
        }

        return $"{a}|{b}|{edge.Weight:0.####}";
    }
}
