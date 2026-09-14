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

    // Raw sequential batches land here as arrays before they are queried as List<T>.
    public RawTelemetryBatches RawBatches { get; } = new();

    public IReadOnlyList<SensorDevice> SnapshotSensors()
    {
        lock (_gate)
        {
            RefreshFreshnessLocked();
            return Sensors.ToList();
        }
    }

    public Task<IReadOnlyList<SensorDevice>> SnapshotSensorsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SnapshotSensors());
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

    public Task<StoreResult> TryAddAsync(SensorDevice device, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var succeeded = TryAdd(device, out var error);
        return Task.FromResult(new StoreResult(succeeded, error));
    }

    public Task<StoreResult> TryIngestEnvironmentalAsync(
        TelemetryPacket<EnvironmentalReading> packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var succeeded = TryIngestEnvironmental(packet, out var error);
        return Task.FromResult(new StoreResult(succeeded, error));
    }

    public Task<StoreResult> TryIngestPowerAsync(
        TelemetryPacket<PowerReading> packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var succeeded = TryIngestPower(packet, out var error);
        return Task.FromResult(new StoreResult(succeeded, error));
    }

    public Task<StoreResult> TryIngestActuatorAsync(
        TelemetryPacket<ActuatorReading> packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var succeeded = TryIngestActuator(packet, out var error);
        return Task.FromResult(new StoreResult(succeeded, error));
    }

    public bool TryIngestEnvironmental(TelemetryPacket<EnvironmentalReading> packet, out string? error)
        => TryIngest(
            packet,
            EnvironmentalPackets,
            (payload, _) => TelemetryHealthClassifier.Classify(payload),
            payload => RawBatches.AppendEnvironmental(packet.DeviceId, payload),
            out error);

    public bool TryIngestPower(TelemetryPacket<PowerReading> packet, out string? error)
        => TryIngest(
            packet,
            PowerPackets,
            (payload, _) => TelemetryHealthClassifier.Classify(payload),
            payload => RawBatches.AppendPower(packet.DeviceId, payload),
            out error);

    public bool TryIngestActuator(TelemetryPacket<ActuatorReading> packet, out string? error)
        => TryIngest(
            packet,
            ActuatorPackets,
            (payload, device) => TelemetryHealthClassifier.Classify(payload, device.ExpectedIsActive),
            payload => RawBatches.AppendActuator(packet.DeviceId, payload),
            out error);

    private bool TryIngest<T>(
        TelemetryPacket<T> packet,
        List<TelemetryPacket<T>> buffer,
        Func<T, SensorDevice, HealthState> classify,
        Action<T> appendRawBatch,
        out string? error)
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

            // Arrays first: sequential per-device history, then the List<T> query buffer.
            appendRawBatch(packet.Payload);
            buffer.Add(packet);
            device.LastSeenAt = packet.Timestamp == default ? DateTimeOffset.UtcNow : packet.Timestamp;
            device.Health = classify(packet.Payload, device);
            device.Freshness = TelemetryFreshnessClassifier.Classify(device.LastSeenAt, DateTimeOffset.UtcNow);
            error = null;
            return true;
        }
    }

    public SensorDevice? FindSensor(string id)
    {
        lock (_gate)
        {
            var device = Sensors.FirstOrDefault(sensor =>
                string.Equals(sensor.Id, id, StringComparison.OrdinalIgnoreCase));
            if (device is not null)
            {
                device.Freshness = TelemetryFreshnessClassifier.Classify(device.LastSeenAt, DateTimeOffset.UtcNow);
            }

            return device;
        }
    }

    public Task<SensorDevice?> FindSensorAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FindSensor(id));
    }

    private void RefreshFreshnessLocked()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var sensor in Sensors)
        {
            sensor.Freshness = TelemetryFreshnessClassifier.Classify(sensor.LastSeenAt, now);
        }
    }

    public TelemetryHistoryResponse HistoryFor(SensorDevice device, int take)
    {
        lock (_gate)
        {
            var history = new TelemetryHistoryResponse
            {
                DeviceId = device.Id,
                Category = device.Category
            };

            switch (device.Category)
            {
                case SensorCategory.Environmental:
                    history.Environmental = Latest(EnvironmentalPackets, device.Id, take);
                    break;
                case SensorCategory.PowerConsumption:
                    history.Power = Latest(PowerPackets, device.Id, take);
                    break;
                case SensorCategory.Actuator:
                    history.Actuator = Latest(ActuatorPackets, device.Id, take);
                    break;
            }

            return history;
        }
    }

    public Task<TelemetryHistoryResponse> HistoryForAsync(
        SensorDevice device,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(HistoryFor(device, take));
    }

    public IReadOnlyList<TelemetryPacket<PowerReading>> SnapshotPowerPackets()
    {
        lock (_gate)
        {
            return PowerPackets.ToList();
        }
    }

    public Task<IReadOnlyList<TelemetryPacket<PowerReading>>> SnapshotPowerPacketsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SnapshotPowerPackets());
    }

    public IReadOnlyList<TelemetryPacket<EnvironmentalReading>> SnapshotEnvironmentalPackets(string deviceId)
    {
        lock (_gate)
        {
            return Latest(EnvironmentalPackets, deviceId, 50);
        }
    }

    public Task<IReadOnlyList<TelemetryPacket<EnvironmentalReading>>> SnapshotEnvironmentalPacketsAsync(
        string deviceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SnapshotEnvironmentalPackets(deviceId));
    }

    private static List<TelemetryPacket<T>> Latest<T>(
        List<TelemetryPacket<T>> buffer,
        string deviceId,
        int take)
        where T : struct
    {
        return buffer
            .Where(packet => string.Equals(packet.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(packet => packet.Timestamp)
            .Take(take)
            .ToList();
    }
}
