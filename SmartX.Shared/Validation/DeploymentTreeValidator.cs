namespace SmartX.Shared;

/// <summary>
/// Recursively checks that a deployment forest is a legal site → zone → sub-zone → node hierarchy.
/// Used when registering a sensor so an ESP32 cannot hang off a broken path.
/// </summary>
public static class DeploymentTreeValidator
{
    /// <summary>
    /// Walks every tree from each site root. Reports all structural problems.
    /// </summary>
    public static ValidationResult ValidateForest(IEnumerable<DeploymentNode> roots)
    {
        var result = new ValidationResult();
        foreach (var root in roots)
        {
            if (root.Level != DeploymentLevel.Site)
            {
                result.AddError($"Deployment root '{root.Id}' must be a Site (facility), not {root.Level}.");
            }

            ValidateNode(root, parent: null, ancestors: [], result);
        }

        return result;
    }

    /// <summary>
    /// Ensures the placement id sits on a complete Site → Zone → SubZone → Node path.
    /// </summary>
    public static ValidationResult ValidateSensorPlacement(
        IEnumerable<DeploymentNode> roots,
        string locationNodeId)
    {
        var result = new ValidationResult();
        if (!TryBuildPath(roots, locationNodeId, out var path))
        {
            result.AddError("Deployment location is not reachable from a site root.");
            return result;
        }

        if (path[^1].Level != DeploymentLevel.Node)
        {
            result.AddError("A sensor must be attached to a node at the end of the deployment path.");
        }

        if (path[0].Level != DeploymentLevel.Site)
        {
            result.AddError("Deployment path must start at a facility/site.");
        }

        for (var i = 1; i < path.Count; i++)
        {
            if ((int)path[i].Level != (int)path[i - 1].Level + 1)
            {
                result.AddError(
                    $"Unsafe path at '{path[i].Name}': expected {(DeploymentLevel)((int)path[i - 1].Level + 1)} under {path[i - 1].Level}, not {path[i].Level}.");
            }
        }

        return result;
    }

    public static string FormatPath(IReadOnlyList<DeploymentNode> path)
        => string.Join(" → ", path.Select(node => node.Name));

    /// <summary>
    /// Recursively validates one node and its children.
    /// Base case: a Node with no children (a rack/bay that can hold sensors).
    /// </summary>
    private static void ValidateNode(
        DeploymentNode node,
        DeploymentNode? parent,
        List<string> ancestors,
        ValidationResult result)
    {
        if (ancestors.Contains(node.Id, StringComparer.OrdinalIgnoreCase))
        {
            result.AddError($"Deployment tree cycle detected at '{node.Id}'.");
            return;
        }

        if (parent is not null && (int)node.Level != (int)parent.Level + 1)
        {
            result.AddError(
                $"'{node.Name}' is {node.Level} under {parent.Level}; children must be the next layer (Site → Zone → SubZone → Node).");
        }

        if (string.IsNullOrWhiteSpace(node.Id) || string.IsNullOrWhiteSpace(node.Name))
        {
            result.AddError("Every deployment node needs an id and a name.");
        }

        // Base case: a mesh Node is a leaf — it must not contain further sites/zones.
        if (node.Level == DeploymentLevel.Node)
        {
            if (node.Children.Count > 0)
            {
                result.AddError($"Node '{node.Name}' must be a leaf (no child locations).");
            }

            return;
        }

        if (node.Children.Count == 0)
        {
            result.AddError($"'{node.Name}' ({node.Level}) has no children, so sensors cannot be placed under it.");
            return;
        }

        var nextAncestors = new List<string>(ancestors) { node.Id };
        foreach (var child in node.Children)
        {
            ValidateNode(child, node, nextAncestors, result);
        }
    }

    private static bool TryBuildPath(
        IEnumerable<DeploymentNode> roots,
        string id,
        out List<DeploymentNode> path)
    {
        path = [];
        foreach (var root in roots)
        {
            if (TryBuildPath(root, id, path))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Recursively searches for id, pushing nodes onto path and popping on backtrack.
    /// Base case: this node is the target, or it has no child that contains the target.
    /// </summary>
    private static bool TryBuildPath(DeploymentNode node, string id, List<DeploymentNode> path)
    {
        path.Add(node);
        if (string.Equals(node.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (TryBuildPath(child, id, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }
}
