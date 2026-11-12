namespace SmartX.Shared;

/// <summary>
/// Graph walk strategy used by <see cref="MeshGraphTraverser"/>.
/// </summary>
public enum MeshTraversalMode
{
    /// <summary>Queue-based level-order walk — natural for hop-distance / healing paths.</summary>
    BreadthFirst = 0,

    /// <summary>Stack-based depth-first walk — useful for exploring one branch before backtracking.</summary>
    DepthFirst = 1
}
