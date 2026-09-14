namespace SmartX.Shared;

/// <summary>
/// Walks the nested site → zone → node forest. Used by registration, location filters,
/// and (later) recursive path validation.
/// </summary>
public static class DeploymentTree
{
    public static DeploymentNode? Find(IEnumerable<DeploymentNode> roots, string id)
    {
        foreach (var root in roots)
        {
            var match = Find(root, id);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Recursively searches one tree. Base case: this id matches, or no child matches.
    /// </summary>
    public static DeploymentNode? Find(DeploymentNode node, string id)
    {
        if (string.Equals(node.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var match = Find(child, id);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Ids of this node and every descendant — so filtering by Zone 1 includes rack nodes under it.
    /// </summary>
    public static HashSet<string> SelfAndDescendantIds(DeploymentNode node)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { node.Id };
        CollectDescendantIds(node, ids);
        return ids;
    }

    private static void CollectDescendantIds(DeploymentNode node, HashSet<string> ids)
    {
        foreach (var child in node.Children)
        {
            ids.Add(child.Id);
            CollectDescendantIds(child, ids);
        }
    }
}
