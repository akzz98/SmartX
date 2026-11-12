namespace SmartX.Shared;

/// <summary>
/// One timestamped telemetry sample stored in the AVL history index.
/// Keyed by device + timestamp (+ sequence) so range queries avoid scanning unsorted lists.
/// </summary>
public sealed class TelemetryHistoryRecord
{
    public string DeviceId { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; }

    public int Sequence { get; init; }

    public SensorCategory Category { get; init; }

    /// <summary>Metric name, e.g. Moisture, Temperature, Watts, Valve.</summary>
    public string Metric { get; init; } = string.Empty;

    public double? NumericValue { get; init; }

    public bool? BoolValue { get; init; }

    public string Summary { get; init; } = string.Empty;

    public static TelemetryHistoryRecord FromEnvironmental(TelemetryPacket<EnvironmentalReading> packet)
        => new()
        {
            DeviceId = packet.DeviceId,
            Timestamp = packet.Timestamp == default ? DateTimeOffset.UtcNow : packet.Timestamp,
            Sequence = packet.Sequence,
            Category = SensorCategory.Environmental,
            Metric = packet.Payload.Metric.ToString(),
            NumericValue = packet.Payload.Value,
            Summary = $"{packet.Payload.Metric}={packet.Payload.Value}"
        };

    public static TelemetryHistoryRecord FromPower(TelemetryPacket<PowerReading> packet)
        => new()
        {
            DeviceId = packet.DeviceId,
            Timestamp = packet.Timestamp == default ? DateTimeOffset.UtcNow : packet.Timestamp,
            Sequence = packet.Sequence,
            Category = SensorCategory.PowerConsumption,
            Metric = packet.Payload.Metric.ToString(),
            NumericValue = packet.Payload.Value,
            Summary = $"{packet.Payload.Metric}={packet.Payload.Value}"
        };

    public static TelemetryHistoryRecord FromActuator(TelemetryPacket<ActuatorReading> packet)
        => new()
        {
            DeviceId = packet.DeviceId,
            Timestamp = packet.Timestamp == default ? DateTimeOffset.UtcNow : packet.Timestamp,
            Sequence = packet.Sequence,
            Category = SensorCategory.Actuator,
            Metric = packet.Payload.Kind.ToString(),
            BoolValue = packet.Payload.IsActive,
            Summary = $"{packet.Payload.Kind}={(packet.Payload.IsActive ? "active" : "inactive")}"
        };
}
