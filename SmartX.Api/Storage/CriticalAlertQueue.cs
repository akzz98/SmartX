using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// Priority path for gateway alerts. Lower rank = higher urgency so Critical work
/// dequeues ahead of Warning/Info and bypasses the standard FIFO telemetry stream.
/// Uses <see cref="PriorityQueue{TElement, TPriority}"/>.
/// </summary>
public sealed class CriticalAlertQueue
{
    private readonly PriorityQueue<PriorityAlert, int> _queue = new();

    public int Depth => _queue.Count;

    public long TotalEnqueued { get; private set; }

    public long TotalDequeued { get; private set; }

    public DateTimeOffset? LastEnqueuedAt { get; private set; }

    public DateTimeOffset? LastDequeuedAt { get; private set; }

    public string? LastBypassReason { get; private set; }

    public void Enqueue(PriorityAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _queue.Enqueue(alert, Rank(alert.Severity));
        TotalEnqueued++;
        LastEnqueuedAt = alert.RaisedAt == default ? DateTimeOffset.UtcNow : alert.RaisedAt;
        if (alert.Severity == AlertSeverity.Critical)
        {
            LastBypassReason = alert.Reason;
        }
    }

    public bool TryDequeue(out PriorityAlert? alert)
    {
        if (!_queue.TryDequeue(out alert, out _))
        {
            alert = null;
            return false;
        }

        TotalDequeued++;
        LastDequeuedAt = DateTimeOffset.UtcNow;
        return true;
    }

    private static int Rank(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => 0,
        AlertSeverity.Warning => 1,
        _ => 2
    };
}
