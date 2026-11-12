namespace SmartX.Shared;

/// <summary>
/// Serializable undirected wireless link for the topology snapshot API.
/// </summary>
public sealed class MeshEdgeResponse
{
    public string FromId { get; set; } = string.Empty;

    public string ToId { get; set; } = string.Empty;

    /// <summary>Routing cost (lower = preferred hop).</summary>
    public double Weight { get; set; }

    public MeshLinkKind LinkKind { get; set; }

    public string? Label { get; set; }
}
