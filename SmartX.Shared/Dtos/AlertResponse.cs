namespace SmartX.Shared;

/// <summary>
/// One priority / live alert as shown on the command-stream dashboard.
/// </summary>
public sealed class AlertResponse
{
    public string Id { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string MacAddress { get; set; } = string.Empty;

    public AlertSeverity Severity { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset RaisedAt { get; set; }

    public bool IsProcessed { get; set; }
}
