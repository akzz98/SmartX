namespace SmartX.Shared;

/// <summary>
/// Per-device AVL history forests. Each ESP32 owns one timestamp-ordered AVL tree so
/// moisture/power/actuator range queries never scan the global unsorted packet lists.
/// </summary>
public sealed class TelemetryHistoryIndex
{
    private readonly Dictionary<string, AvlTelemetryHistoryTree> _trees =
        new(StringComparer.OrdinalIgnoreCase);

    public int DeviceTreeCount => _trees.Count;

    public int TotalSampleCount => _trees.Values.Sum(tree => tree.Count);

    public void Insert(TelemetryHistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.DeviceId))
        {
            return;
        }

        var id = record.DeviceId.Trim();
        if (!_trees.TryGetValue(id, out var tree))
        {
            tree = new AvlTelemetryHistoryTree();
            _trees[id] = tree;
        }

        tree.Insert(record);
    }

    public AvlTelemetryHistoryTree? GetTree(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return _trees.TryGetValue(deviceId.Trim(), out var tree) ? tree : null;
    }

    public IReadOnlyList<TelemetryHistoryRecord> QueryRange(
        string deviceId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? metric,
        out int nodesVisited,
        out int treeSize)
    {
        nodesVisited = 0;
        treeSize = 0;
        var tree = GetTree(deviceId);
        if (tree is null)
        {
            return [];
        }

        treeSize = tree.Count;
        return tree.QueryRange(from, to, metric, out nodesVisited);
    }
}
