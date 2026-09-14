namespace SmartX.Shared;

/// <summary>
/// Generic telemetry envelope. T stays a value type (float, int, bool) so readings
/// are not boxed into object when the gateway ingests mixed ESP32 payloads.
/// </summary>
public sealed class TelemetryPacket<T> where T : struct
{
    public string DeviceId { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    public int Sequence { get; set; }

    /// <summary>Optional link quality (for example RSSI-style 0–100). Missing means unknown.</summary>
    public byte? SignalQuality { get; set; }

    public T Payload { get; set; }
}
