namespace SmartX.Shared;

/// <summary>
/// How urgently a gateway alert must be processed. Critical items bypass the standard
/// FIFO telemetry queue via the Part 2 priority path (e.g. moisture crash, power spike).
/// </summary>
public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}
