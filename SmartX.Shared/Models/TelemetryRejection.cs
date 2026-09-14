namespace SmartX.Shared;

/// <summary>
/// A packet the gateway refused. It is not stored as telemetry, so history stays typed and valid.
/// </summary>
public sealed class TelemetryRejection
{
    public string DeviceId { get; set; } = string.Empty;

    public DateTimeOffset RejectedAt { get; set; }

    public List<string> Errors { get; set; } = [];
}
