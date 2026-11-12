namespace SmartX.Shared;

/// <summary>
/// Reachable vs down / cut-off classification from a gateway-rooted walk
/// (Final PoE Stage 5.2).
/// </summary>
public sealed class MeshReachabilityReport
{
    public required string RootId { get; init; }

    public required MeshTraversalMode Mode { get; init; }

    /// <summary>Online-only discovery order (live routing subnet from the root).</summary>
    public required IReadOnlyList<string> VisitOrder { get; init; }

    /// <summary>Vertices reachable from the root through online nodes only.</summary>
    public required IReadOnlyList<string> ReachableOnlineIds { get; init; }

    /// <summary>Vertices with <see cref="MeshNode.IsOnline"/> == false.</summary>
    public required IReadOnlyList<string> DownNodeIds { get; init; }

    /// <summary>
    /// Online vertices that topology can reach but live routing cannot
    /// (cut off behind a down relay).
    /// </summary>
    public required IReadOnlyList<string> CutOffOnlineIds { get; init; }

    /// <summary>
    /// Everything not in <see cref="ReachableOnlineIds"/> (down nodes + cut-offs).
    /// </summary>
    public required IReadOnlyList<string> UnreachableIds { get; init; }

    /// <summary>Per-down-node next-hop / alternate path suggestions.</summary>
    public required IReadOnlyList<MeshHopSuggestion> DownNodeSuggestions { get; init; }
}
