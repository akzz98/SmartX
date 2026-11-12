using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// Priority path for gateway alerts. Lower composite key = higher urgency so Critical work
/// dequeues ahead of Warning/Info and bypasses the standard FIFO telemetry stream.
/// Uses <see cref="PriorityQueue{TElement, TPriority}"/>.
/// <para>
/// Tie-breaker: equal <see cref="AlertSeverity"/> → earlier <see cref="PriorityAlert.RaisedAt"/> wins
/// (older alert is processed first). Severity rank is the primary key; UTC ticks are secondary.
/// </para>
/// </summary>
public sealed class CriticalAlertQueue
{
    private readonly PriorityQueue<PriorityAlert, PriorityKey> _queue = new();

    public int Depth => _queue.Count;

    public long TotalEnqueued { get; private set; }

    public long TotalDequeued { get; private set; }

    public DateTimeOffset? LastEnqueuedAt { get; private set; }

    public DateTimeOffset? LastDequeuedAt { get; private set; }

    public string? LastBypassReason { get; private set; }

    public void Enqueue(PriorityAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        var raisedAt = alert.RaisedAt == default ? DateTimeOffset.UtcNow : alert.RaisedAt;
        if (alert.RaisedAt == default)
        {
            alert.RaisedAt = raisedAt;
        }

        _queue.Enqueue(alert, PriorityKey.From(alert.Severity, raisedAt));
        TotalEnqueued++;
        LastEnqueuedAt = raisedAt;
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

    /// <summary>
    /// Primary: severity rank (Critical=0 … Info=2). Secondary: earlier RaisedAt first.
    /// </summary>
    private readonly record struct PriorityKey(int SeverityRank, long RaisedAtUtcTicks) : IComparable<PriorityKey>
    {
        public static PriorityKey From(AlertSeverity severity, DateTimeOffset raisedAt)
            => new(Rank(severity), raisedAt.UtcTicks);

        public int CompareTo(PriorityKey other)
        {
            var bySeverity = SeverityRank.CompareTo(other.SeverityRank);
            return bySeverity != 0
                ? bySeverity
                : RaisedAtUtcTicks.CompareTo(other.RaisedAtUtcTicks);
        }

        private static int Rank(AlertSeverity severity) => severity switch
        {
            AlertSeverity.Critical => 0,
            AlertSeverity.Warning => 1,
            _ => 2
        };
    }
}
