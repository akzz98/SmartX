using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// In-memory gateway state for the simulation. A singleton so every request
/// sees the same registered ESP32 devices until the process restarts.
/// </summary>
public sealed class GatewayStore
{
    private readonly object _gate = new();

    public GatewayStore()
    {
        DeploymentRoots = DefaultDeploymentTree.Create();
    }

    public List<DeploymentNode> DeploymentRoots { get; }

    public List<SensorDevice> Sensors { get; } = [];

    // Separate typed buffers — never List<object> — so float/int/bool packets stay unboxed.
    public List<TelemetryPacket<EnvironmentalReading>> EnvironmentalPackets { get; } = [];

    public List<TelemetryPacket<PowerReading>> PowerPackets { get; } = [];

    public List<TelemetryPacket<ActuatorReading>> ActuatorPackets { get; } = [];

    public IReadOnlyList<SensorDevice> SnapshotSensors()
    {
        lock (_gate)
        {
            return Sensors.ToList();
        }
    }

    public bool TryAdd(SensorDevice device, out string? error)
    {
        lock (_gate)
        {
            if (Sensors.Any(existing => string.Equals(existing.MacAddress, device.MacAddress, StringComparison.OrdinalIgnoreCase)))
            {
                error = "A sensor with this MAC address is already registered.";
                return false;
            }

            if (Sensors.Any(existing => string.Equals(existing.Id, device.Id, StringComparison.OrdinalIgnoreCase)))
            {
                error = "A sensor with this device id is already registered.";
                return false;
            }

            Sensors.Add(device);
            error = null;
            return true;
        }
    }

    public bool TryIngestEnvironmental(TelemetryPacket<EnvironmentalReading> packet, out string? error)
        => TryIngest(packet, EnvironmentalPackets, out error);

    public bool TryIngestPower(TelemetryPacket<PowerReading> packet, out string? error)
        => TryIngest(packet, PowerPackets, out error);

    public bool TryIngestActuator(TelemetryPacket<ActuatorReading> packet, out string? error)
        => TryIngest(packet, ActuatorPackets, out error);

    private bool TryIngest<T>(TelemetryPacket<T> packet, List<TelemetryPacket<T>> buffer, out string? error)
        where T : struct
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(packet.DeviceId))
            {
                error = "Packet device id is required.";
                return false;
            }

            var device = Sensors.FirstOrDefault(sensor =>
                string.Equals(sensor.Id, packet.DeviceId, StringComparison.OrdinalIgnoreCase));

            if (device is null)
            {
                error = "Device is not registered on this gateway.";
                return false;
            }

            buffer.Add(packet);
            device.LastSeenAt = packet.Timestamp == default ? DateTimeOffset.UtcNow : packet.Timestamp;
            error = null;
            return true;
        }
    }
}
