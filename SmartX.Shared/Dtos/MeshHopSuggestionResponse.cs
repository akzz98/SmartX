namespace SmartX.Shared;

/// <summary>
/// API DTO for a down-node healing hint (ideal path + alternate next hop).
/// </summary>
public sealed class MeshHopSuggestionResponse
{
    public string DownNodeId { get; set; } = string.Empty;

    public List<string> IdealPathFromRoot { get; set; } = [];

    public List<string> OnlineNeighbourIds { get; set; } = [];

    public string? SuggestedNextHop { get; set; }

    public List<string> AlternatePathToNextHop { get; set; } = [];
}
