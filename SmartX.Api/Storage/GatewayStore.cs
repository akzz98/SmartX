using SmartX.Api.Simulation;
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
        var tree = DeploymentTreeValidator.ValidateForest(DeploymentRoots);
        if (!tree.IsValid)
        {
            throw new InvalidOperationException(
                "Default deployment tree failed recursive validation: " + string.Join(" ", tree.Errors));
        }

        FleetSeeder.SeedDevices(this);
        TelemetryStreamSeeder.SeedNormalStreams(this);
        TelemetryFaultSeeder.SeedTemperatureSpike(this);
        TelemetryFaultSeeder.SeedStuckActuator(this);
        TelemetryFaultSeeder.SeedSilentSensor(this);
        TelemetryFaultSeeder.SeedMalformedPackets(this);
        TelemetryFaultSeeder.SeedLocationOutage(this);
        TelemetryFaultSeeder.SeedRecovery(this);
    }

    public List<DeploymentNode> DeploymentRoots { get; }

    /// <summary>Custom fleet collection: lookup + ingest counters, not a bare List.</summary>
    public SensorFleet Fleet { get; } = new();

    // Separate typed buffers — never List<object> — so float/int/bool packets stay unboxed.
    public List<TelemetryPacket<EnvironmentalReading>> EnvironmentalPackets { get; } = [];

    public List<TelemetryPacket<PowerReading>> PowerPackets { get; } = [];

    public List<TelemetryPacket<ActuatorReading>> ActuatorPackets { get; } = [];

    // Flattened numeric lists copied from jagged/window arrays for collection-based queries.
    public List<float> PromotedEnvironmentalValues { get; } = [];

    public List<int> PromotedPowerValues { get; } = [];

    public List<bool> PromotedActuatorValues { get; } = [];

    // Raw sequential batches land here as arrays before they are queried as List<T>.
    public RawTelemetryBatches RawBatches { get; } = new();

    // Refused packets stay out of the typed buffers; operators still see that garbage arrived.
    public List<TelemetryRejection> Rejections { get; } = [];

    public IReadOnlyList<SensorDevice> SnapshotSensors()
    {
        lock (_gate)
        {
            RefreshFreshnessLocked();
            return Fleet.Snapshot();
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
            return Fleet.TryAdd(device, out error);
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
            payload => RawBatches.AppendEnvironmental(packet),
            RawBatches.PromoteEnvironmentalPackets,
            out error);

    public bool TryIngestPower(TelemetryPacket<PowerReading> packet, out string? error)
        => TryIngest(
            packet,
            PowerPackets,
            (payload, _) => TelemetryHealthClassifier.Classify(payload),
            payload => RawBatches.AppendPower(packet),
            RawBatches.PromotePowerPackets,
            out error);

    public bool TryIngestActuator(TelemetryPacket<ActuatorReading> packet, out string? error)
        => TryIngest(
            packet,
            ActuatorPackets,
            (payload, device) => TelemetryHealthClassifier.Classify(payload, device.ExpectedIsActive),
            payload => RawBatches.AppendActuator(packet),
            RawBatches.PromoteActuatorPackets,
            out error);

    private bool TryIngest<T>(
        TelemetryPacket<T> packet,
        List<TelemetryPacket<T>> buffer,
        Func<T, SensorDevice, HealthState> classify,
        Action<T> appendRawBatch,
        Func<List<TelemetryPacket<T>>> promoteToList,
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

            var device = Fleet.FindById(packet.DeviceId);

            if (device is null)
            {
                error = "Device is not registered on this gateway.";
                return false;
            }

            // Arrays first, then copy the sequential batch into List<T> for queries.
            appendRawBatch(packet.Payload);
            TelemetryCollectionPromoter.Replace(buffer, promoteToList());
            TelemetryCollectionPromoter.Replace(PromotedEnvironmentalValues, RawBatches.PromoteEnvironmentalValues());
            TelemetryCollectionPromoter.Replace(PromotedPowerValues, RawBatches.PromotePowerValues());
            TelemetryCollectionPromoter.Replace(PromotedActuatorValues, RawBatches.PromoteActuatorValues());
            device.LastSeenAt = packet.Timestamp == default ? DateTimeOffset.UtcNow : packet.Timestamp;
            device.Health = classify(packet.Payload, device);
            device.Freshness = TelemetryFreshnessClassifier.Classify(device.LastSeenAt, DateTimeOffset.UtcNow);
            Fleet.RecordIngest(device.Id, packet.Sequence, device.LastSeenAt.Value);
            error = null;
            return true;
        }
    }

    public SensorDevice? FindSensor(string id)
    {
        lock (_gate)
        {
            var device = Fleet.FindById(id);
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

    public DeviceIngestState? GetIngestState(string id)
    {
        lock (_gate)
        {
            return Fleet.GetIngestState(id);
        }
    }

    public Task<DeviceIngestState?> GetIngestStateAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetIngestState(id));
    }

    /// <summary>
    /// Records a validator rejection. Last-seen and history do not change — a CRC failure is not a heartbeat.
    /// A known device is marked Invalid so the overview can show the exception.
    /// </summary>
    public void RecordRejection(string deviceId, IEnumerable<string> errors)
    {
        lock (_gate)
        {
            Rejections.Add(new TelemetryRejection
            {
                DeviceId = deviceId,
                RejectedAt = DateTimeOffset.UtcNow,
                Errors = [.. errors]
            });

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            var device = Fleet.FindById(deviceId);
            if (device is not null)
            {
                device.Health = HealthState.Invalid;
            }
        }
    }

    public Task RecordRejectionAsync(string deviceId, IEnumerable<string> errors, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RecordRejection(deviceId, errors);
        return Task.CompletedTask;
    }

    public IReadOnlyList<TelemetryRejection> SnapshotRejections(string? deviceId = null)
    {
        lock (_gate)
        {
            IEnumerable<TelemetryRejection> rows = Rejections;
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                rows = rows.Where(row =>
                    string.Equals(row.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            }

            return rows.ToList();
        }
    }

    public Task<IReadOnlyList<TelemetryRejection>> SnapshotRejectionsAsync(
        string? deviceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SnapshotRejections(deviceId));
    }

    private void RefreshFreshnessLocked()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var sensor in Fleet)
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
