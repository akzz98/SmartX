namespace SmartX.Shared;

/// <summary>
/// Self-balancing AVL tree of <see cref="TelemetryHistoryRecord"/> ordered by timestamp
/// (then sequence). Range queries walk only the overlapping subtree — O(log n + k) —
/// instead of scanning an unsorted list of all samples for a device.
/// </summary>
public sealed class AvlTelemetryHistoryTree
{
    private Node? _root;

    public int Count { get; private set; }

    public void Insert(TelemetryHistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _root = Insert(_root, record);
        Count++;
    }

    /// <summary>
    /// Collects samples with timestamp in [from, to] (inclusive), optionally filtered by metric.
    /// <paramref name="nodesVisited"/> counts AVL nodes examined (efficiency evidence vs full scan).
    /// </summary>
    public IReadOnlyList<TelemetryHistoryRecord> QueryRange(
        DateTimeOffset from,
        DateTimeOffset to,
        string? metric,
        out int nodesVisited)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var results = new List<TelemetryHistoryRecord>();
        nodesVisited = 0;
        CollectRange(_root, from, to, NormalizeMetric(metric), results, ref nodesVisited);
        return results;
    }

    public DateTimeOffset? MinTimestamp => MinNode(_root)?.Record.Timestamp;

    public DateTimeOffset? MaxTimestamp => MaxNode(_root)?.Record.Timestamp;

    private static Node Insert(Node? node, TelemetryHistoryRecord record)
    {
        if (node is null)
        {
            return new Node(record);
        }

        var cmp = Compare(record, node.Record);
        if (cmp < 0)
        {
            node.Left = Insert(node.Left, record);
        }
        else if (cmp > 0)
        {
            node.Right = Insert(node.Right, record);
        }
        else
        {
            // Identical key — keep both by pushing the new sample right (stable under range).
            node.Right = Insert(node.Right, record);
        }

        UpdateHeight(node);
        return Balance(node);
    }

    private static void CollectRange(
        Node? node,
        DateTimeOffset from,
        DateTimeOffset to,
        string? metric,
        List<TelemetryHistoryRecord> results,
        ref int nodesVisited)
    {
        if (node is null)
        {
            return;
        }

        nodesVisited++;
        var ts = node.Record.Timestamp;

        // Left subtree can only hold earlier timestamps.
        if (ts >= from)
        {
            CollectRange(node.Left, from, to, metric, results, ref nodesVisited);
        }

        if (ts >= from && ts <= to && MetricMatches(node.Record, metric))
        {
            results.Add(node.Record);
        }

        if (ts <= to)
        {
            CollectRange(node.Right, from, to, metric, results, ref nodesVisited);
        }
    }

    private static bool MetricMatches(TelemetryHistoryRecord record, string? metric)
    {
        if (string.IsNullOrWhiteSpace(metric))
        {
            return true;
        }

        return record.Metric.Equals(metric, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeMetric(string? metric)
        => string.IsNullOrWhiteSpace(metric) ? null : metric.Trim();

    private static int Compare(TelemetryHistoryRecord a, TelemetryHistoryRecord b)
    {
        var byTime = a.Timestamp.CompareTo(b.Timestamp);
        return byTime != 0 ? byTime : a.Sequence.CompareTo(b.Sequence);
    }

    private static Node Balance(Node node)
    {
        var balance = BalanceFactor(node);
        if (balance > 1)
        {
            if (BalanceFactor(node.Left) < 0)
            {
                node.Left = RotateLeft(node.Left!);
            }

            return RotateRight(node);
        }

        if (balance < -1)
        {
            if (BalanceFactor(node.Right) > 0)
            {
                node.Right = RotateRight(node.Right!);
            }

            return RotateLeft(node);
        }

        return node;
    }

    private static Node RotateRight(Node y)
    {
        var x = y.Left!;
        y.Left = x.Right;
        x.Right = y;
        UpdateHeight(y);
        UpdateHeight(x);
        return x;
    }

    private static Node RotateLeft(Node x)
    {
        var y = x.Right!;
        x.Right = y.Left;
        y.Left = x;
        UpdateHeight(x);
        UpdateHeight(y);
        return y;
    }

    private static void UpdateHeight(Node node)
        => node.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));

    private static int Height(Node? node) => node?.Height ?? 0;

    private static int BalanceFactor(Node? node)
        => node is null ? 0 : Height(node.Left) - Height(node.Right);

    private static Node? MinNode(Node? node)
    {
        while (node?.Left is not null)
        {
            node = node.Left;
        }

        return node;
    }

    private static Node? MaxNode(Node? node)
    {
        while (node?.Right is not null)
        {
            node = node.Right;
        }

        return node;
    }

    private sealed class Node(TelemetryHistoryRecord record)
    {
        public TelemetryHistoryRecord Record { get; } = record;
        public Node? Left { get; set; }
        public Node? Right { get; set; }
        public int Height { get; set; } = 1;
    }
}
