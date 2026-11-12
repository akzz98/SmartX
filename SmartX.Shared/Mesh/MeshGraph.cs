namespace SmartX.Shared;

/// <summary>
/// Undirected IoT mesh as an adjacency list: vertex id → incident edges.
/// Vertices are ESP32 sensors plus the central gateway; edges are wireless hops
/// with a power/RSSI cost for later BFS/DFS and MST (Final PoE Stage 4+).
/// </summary>
public sealed class MeshGraph
{
    public const string GatewayNodeId = "sx-gateway";

    private readonly Dictionary<string, MeshNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<MeshEdge>> _adjacency = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MeshEdge> _edges = [];

    public int NodeCount => _nodes.Count;

    public int EdgeCount => _edges.Count;

    public IReadOnlyCollection<MeshNode> Nodes => _nodes.Values;

    public IReadOnlyList<MeshEdge> Edges => _edges;

    public bool TryGetNode(string id, out MeshNode? node)
    {
        if (_nodes.TryGetValue(id, out var found))
        {
            node = found;
            return true;
        }

        node = null;
        return false;
    }

    public MeshNode GetNode(string id)
        => _nodes.TryGetValue(id, out var node)
            ? node
            : throw new KeyNotFoundException($"Mesh node '{id}' is not in the graph.");

    /// <summary>Incident edges for a vertex (empty if unknown id).</summary>
    public IReadOnlyList<MeshEdge> Neighbours(string nodeId)
        => _adjacency.TryGetValue(nodeId, out var list) ? list : Array.Empty<MeshEdge>();

    public void AddNode(MeshNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            throw new ArgumentException("Mesh node id is required.", nameof(node));
        }

        var id = node.Id.Trim();
        if (_nodes.ContainsKey(id))
        {
            throw new InvalidOperationException($"Mesh node '{id}' already exists.");
        }

        _nodes[id] = node;
        _adjacency[id] = [];
    }

    /// <summary>
    /// Adds an undirected edge once to both adjacency lists.
    /// </summary>
    public void AddUndirectedEdge(MeshEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        if (string.IsNullOrWhiteSpace(edge.FromId) || string.IsNullOrWhiteSpace(edge.ToId))
        {
            throw new ArgumentException("Edge endpoints are required.", nameof(edge));
        }

        if (edge.Weight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(edge), "Edge weight must be non-negative.");
        }

        if (!_nodes.ContainsKey(edge.FromId) || !_nodes.ContainsKey(edge.ToId))
        {
            throw new InvalidOperationException(
                $"Both endpoints must exist before linking ({edge.FromId} ↔ {edge.ToId}).");
        }

        if (string.Equals(edge.FromId, edge.ToId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Self-loops are not allowed in the mesh.");
        }

        _edges.Add(edge);
        _adjacency[edge.FromId].Add(edge);
        _adjacency[edge.ToId].Add(edge);
    }

    public void Clear()
    {
        _nodes.Clear();
        _adjacency.Clear();
        _edges.Clear();
    }
}
