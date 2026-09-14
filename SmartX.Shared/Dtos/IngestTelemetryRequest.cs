namespace SmartX.Shared;

/// <summary>
/// HTTP body for ingesting one typed packet.
/// T must be a struct payload (EnvironmentalReading, PowerReading, or ActuatorReading).
/// Do not use object or a non-generic packet — that would box mixed ESP32 values.
/// The API will expose a closed generic per category so JSON can deserialize T correctly.
/// </summary>
public sealed class IngestTelemetryRequest<T> where T : struct
{
    public TelemetryPacket<T> Packet { get; set; } = new();
}
