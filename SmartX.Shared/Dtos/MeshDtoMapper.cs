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
}
