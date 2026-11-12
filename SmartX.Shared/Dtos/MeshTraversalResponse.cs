namespace SmartX.Shared;

/// <summary>
/// API result for BFS mesh traversal from a root: visit order, down / cut-off nodes,
/// and suggested hops (Final PoE Stage 5.3).
/// </summary>
public sealed class MeshTraversalResponse
{
    public string RootId { get; set; } = string.Empty;

    public MeshTraversalMode Mode { get; set; }

    public List<string> VisitOrder { get; set; } = [];

    public List<string> ReachableOnlineIds { get; set; } = [];

    public List<string> DownNodeIds { get; set; } = [];

    public List<string> CutOffOnlineIds { get; set; } = [];

    public List<string> UnreachableIds { get; set; } = [];

    public List<MeshHopSuggestionResponse> DownNodeSuggestions { get; set; } = [];
}
