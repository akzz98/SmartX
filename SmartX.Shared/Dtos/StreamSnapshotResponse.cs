namespace SmartX.Shared;

/// <summary>
/// Operator snapshot of the real-time processing engine: queue depths, last work, and recent activity.
/// Used to prove FIFO vs priority-bypass behaviour on the command-stream page and in the README.
/// </summary>
public sealed class StreamSnapshotResponse
{
    /// <summary>Packets waiting in the standard FIFO ingest queue.</summary>
    public int StandardQueueDepth { get; set; }

    /// <summary>Critical alerts waiting in the priority bypass path.</summary>
    public int PriorityQueueDepth { get; set; }

    /// <summary>Manual overrides currently on the undo stack.</summary>
    public int UndoStackDepth { get; set; }

    /// <summary>Unique active error / disconnect keys tracked in the HashSet.</summary>
    public int UniqueActiveAlertCount { get; set; }

    public DateTimeOffset? LastStandardProcessedAt { get; set; }

    public DateTimeOffset? LastPriorityProcessedAt { get; set; }

    public string? LastPriorityBypassReason { get; set; }

    public List<CommandResponse> RecentCommands { get; set; } = [];

    public List<AlertResponse> RecentAlerts { get; set; } = [];
}
