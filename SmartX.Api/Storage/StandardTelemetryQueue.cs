namespace SmartX.Api.Storage;

/// <summary>
/// Standard First-In-First-Out path for normal telemetry hitting the gateway.
/// Packets enqueue here before they are dequeued into typed history / fleet state.
/// Critical alerts use a separate priority path (Stage 3.3) and bypass this queue.
/// </summary>
public sealed class StandardTelemetryQueue
{
    private readonly Queue<QueuedTelemetryItem> _queue = new();

    public int Depth => _queue.Count;

    /// <summary>Highest FIFO depth observed (e.g. after a seeded burst before drain).</summary>
    public int PeakDepth { get; private set; }

    public long TotalEnqueued { get; private set; }

    public long TotalDequeued { get; private set; }

    public DateTimeOffset? LastEnqueuedAt { get; private set; }

    public DateTimeOffset? LastDequeuedAt { get; private set; }

    public void Enqueue(QueuedTelemetryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _queue.Enqueue(item);
        TotalEnqueued++;
        LastEnqueuedAt = item.EnqueuedAt;
        if (_queue.Count > PeakDepth)
        {
            PeakDepth = _queue.Count;
        }
    }

    public bool TryDequeue(out QueuedTelemetryItem? item)
    {
        if (_queue.Count == 0)
        {
            item = null;
            return false;
        }

        item = _queue.Dequeue();
        TotalDequeued++;
        LastDequeuedAt = DateTimeOffset.UtcNow;
        return true;
    }
}
