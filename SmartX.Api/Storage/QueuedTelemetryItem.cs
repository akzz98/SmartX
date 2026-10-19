using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// One pending packet on the standard FIFO ingest queue. Category selects which
/// typed packet field is set so float/int/bool payloads are not boxed into object.
/// </summary>
public sealed class QueuedTelemetryItem
{
    public SensorCategory Category { get; init; }

    public DateTimeOffset EnqueuedAt { get; init; }

    public TelemetryPacket<EnvironmentalReading>? Environmental { get; init; }

    public TelemetryPacket<PowerReading>? Power { get; init; }

    public TelemetryPacket<ActuatorReading>? Actuator { get; init; }

    public static QueuedTelemetryItem FromEnvironmental(TelemetryPacket<EnvironmentalReading> packet)
        => new()
        {
            Category = SensorCategory.Environmental,
            EnqueuedAt = DateTimeOffset.UtcNow,
            Environmental = packet
        };

    public static QueuedTelemetryItem FromPower(TelemetryPacket<PowerReading> packet)
        => new()
        {
            Category = SensorCategory.PowerConsumption,
            EnqueuedAt = DateTimeOffset.UtcNow,
            Power = packet
        };

    public static QueuedTelemetryItem FromActuator(TelemetryPacket<ActuatorReading> packet)
        => new()
        {
            Category = SensorCategory.Actuator,
            EnqueuedAt = DateTimeOffset.UtcNow,
            Actuator = packet
        };
}
