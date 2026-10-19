namespace SmartX.Shared;

/// <summary>
/// A volatile gateway alert raised from telemetry or connectivity. Critical severity
/// enters the priority queue so it is processed ahead of the standard FIFO stream.
/// </summary>
public sealed class PriorityAlert
{
    public string Id { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string MacAddress { get; set; } = string.Empty;

    public AlertSeverity Severity { get; set; }

    /// <summary>Operator-facing cause, e.g. critical moisture drop or severe power spike.</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset RaisedAt { get; set; }

    /// <summary>True once the priority (or FIFO) worker has finished handling this alert.</summary>
    public bool IsProcessed { get; set; }
}
