namespace SmartX.Shared;

/// <summary>
/// Healing hint for a down mesh vertex: ideal path (status ignored) plus an
/// alternate next hop that is still online and reachable from the gateway.
/// </summary>
public sealed class MeshHopSuggestion
{
    public required string DownNodeId { get; init; }

    /// <summary>
    /// Discovery path from the root if every vertex were forwarding (topology only).
    /// Empty when the down node is disconnected even ignoring online status.
    /// </summary>
    public required IReadOnlyList<string> IdealPathFromRoot { get; init; }

    /// <summary>Wireless neighbours that are still online.</summary>
    public required IReadOnlyList<string> OnlineNeighbourIds { get; init; }

    /// <summary>
    /// Preferred alternate hop: an online neighbour still reachable from the root
    /// via live nodes (shortest BFS hop count). Null when no healing neighbour exists.
    /// </summary>
    public string? SuggestedNextHop { get; init; }

    /// <summary>
    /// Live path from the root to <see cref="SuggestedNextHop"/> (entry point around the fault).
    /// </summary>
    public IReadOnlyList<string> AlternatePathToNextHop { get; init; } = Array.Empty<string>();
}
