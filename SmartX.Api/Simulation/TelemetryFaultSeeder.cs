using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Simulation;

/// <summary>
/// Overlay fault packets on the healthy fleet. Each method is one checklist scenario
/// so spikes, stuck actuators, and dropouts stay independent.
/// </summary>
public static class TelemetryFaultSeeder
{
    public const string TemperatureSpikeDeviceId = "sx-env-rack1-temp";

    // Climb out of the 18–26 °C crop band; 44 °C is Critical but still a valid packet (≤ 80).
    private static readonly float[] TemperatureSpikeCelsius = [28f, 36f, 44f];

    public static void SeedTemperatureSpike(GatewayStore store)
    {
        var device = store.FindSensor(TemperatureSpikeDeviceId)
            ?? throw new InvalidOperationException($"Temperature spike target '{TemperatureSpikeDeviceId}' is not registered.");

        var ingest = store.GetIngestState(device.Id)
            ?? throw new InvalidOperationException($"No ingest state for '{device.Id}' — seed the normal stream first.");

        var timestamp = (ingest.LastIngestedAt ?? DateTimeOffset.UtcNow).AddSeconds(30);
        var sequence = ingest.LastSequence;

        foreach (var celsius in TemperatureSpikeCelsius)
        {
            sequence++;
            timestamp = timestamp.AddSeconds(30);
            var packet = new TelemetryPacket<EnvironmentalReading>
            {
                DeviceId = device.Id,
                Timestamp = timestamp,
                Sequence = sequence,
                SignalQuality = 88,
                Payload = new EnvironmentalReading(celsius, EnvironmentalMetric.Temperature)
            };

            var validation = TelemetryPacketValidator.Validate(packet, device);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Temperature spike packet rejected for '{device.Id}' sequence {sequence}: {string.Join(" ", validation.Errors)}");
            }

            if (!store.TryIngestEnvironmental(packet, out var error))
            {
                throw new InvalidOperationException($"Temperature spike ingest failed for '{device.Id}': {error}");
            }
        }
    }

    public const string StuckValveDeviceId = "sx-act-rack2-valve";

    /// <summary>
    /// Rack 2 irrigation valve is commanded open but keeps reporting closed (stuck shut).
    /// </summary>
    public static void SeedStuckActuator(GatewayStore store)
    {
        var device = store.FindSensor(StuckValveDeviceId)
            ?? throw new InvalidOperationException($"Stuck actuator target '{StuckValveDeviceId}' is not registered.");

        if (device.ExpectedIsActive is not true)
        {
            throw new InvalidOperationException($"'{device.Id}' must be commanded open so a closed reading is a stuck-valve fault.");
        }

        var ingest = store.GetIngestState(device.Id)
            ?? throw new InvalidOperationException($"No ingest state for '{device.Id}' — seed the normal stream first.");

        var timestamp = (ingest.LastIngestedAt ?? DateTimeOffset.UtcNow).AddSeconds(30);
        var sequence = ingest.LastSequence;

        // Several identical closed readings so this is a stuck GPIO, not a one-sample glitch.
        for (var i = 0; i < 3; i++)
        {
            sequence++;
            timestamp = timestamp.AddSeconds(30);
            var packet = new TelemetryPacket<ActuatorReading>
            {
                DeviceId = device.Id,
                Timestamp = timestamp,
                Sequence = sequence,
                SignalQuality = 90,
                Payload = new ActuatorReading(isActive: false, ActuatorKind.Valve)
            };

            var validation = TelemetryPacketValidator.Validate(packet, device);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Stuck actuator packet rejected for '{device.Id}' sequence {sequence}: {string.Join(" ", validation.Errors)}");
            }

            if (!store.TryIngestActuator(packet, out var error))
            {
                throw new InvalidOperationException($"Stuck actuator ingest failed for '{device.Id}': {error}");
            }
        }
    }

    public const string SilentSensorDeviceId = "sx-env-sump-temp";

    /// <summary>
    /// Sump temperature transmitted, then went quiet. Last sample is older than the
    /// stale window so the classifier is already at Disconnected (it passed Aging and Stale).
    /// </summary>
    public static void SeedSilentSensor(GatewayStore store)
    {
        var device = store.FindSensor(SilentSensorDeviceId)
            ?? throw new InvalidOperationException($"Silent sensor target '{SilentSensorDeviceId}' is not registered.");

        var ingest = store.GetIngestState(device.Id);
        if (ingest is { PacketCount: > 0 })
        {
            throw new InvalidOperationException($"'{device.Id}' already has packets; the dropout stream would not be last-seen.");
        }

        // Past StaleWindow so a GET immediately shows Disconnected, not Stale.
        var lastSampleAt = DateTimeOffset.UtcNow
            - TelemetryFreshnessClassifier.StaleWindow
            - TimeSpan.FromMinutes(5);

        TelemetryStreamSeeder.SeedHealthyStream(store, device, lastSampleAt, new Random(7112));
    }

    public const string MalformedPacketDeviceId = "sx-env-rack3-ph";

    /// <summary>
    /// Runs garbage ESP32 frames through the same validator as POST /api/telemetry.
    /// Rejected packets are not ingested; the device is marked Invalid.
    /// </summary>
    public static void SeedMalformedPackets(GatewayStore store)
    {
        var now = DateTimeOffset.UtcNow;
        RejectEnvironmental(store, new TelemetryPacket<EnvironmentalReading>
        {
            DeviceId = MalformedPacketDeviceId,
            Timestamp = now,
            Sequence = 200,
            Payload = new EnvironmentalReading(16.5f, EnvironmentalMetric.Ph)
        });
        RejectEnvironmental(store, new TelemetryPacket<EnvironmentalReading>
        {
            DeviceId = MalformedPacketDeviceId,
            Timestamp = now,
            Sequence = 201,
            Payload = new EnvironmentalReading(float.NaN, EnvironmentalMetric.Ph)
        });
        RejectEnvironmental(store, new TelemetryPacket<EnvironmentalReading>
        {
            DeviceId = MalformedPacketDeviceId,
            Timestamp = default,
            Sequence = 202,
            Payload = new EnvironmentalReading(6.0f, EnvironmentalMetric.Ph)
        });
        RejectEnvironmental(store, new TelemetryPacket<EnvironmentalReading>
        {
            DeviceId = string.Empty,
            Timestamp = now,
            Sequence = 1,
            Payload = new EnvironmentalReading(22f, EnvironmentalMetric.Temperature)
        });
        RejectEnvironmental(store, new TelemetryPacket<EnvironmentalReading>
        {
            DeviceId = "sx-ghost-node",
            Timestamp = now,
            Sequence = 1,
            Payload = new EnvironmentalReading(22f, EnvironmentalMetric.Temperature)
        });

        var envDevice = store.FindSensor(MalformedPacketDeviceId);
        var mismatched = new TelemetryPacket<PowerReading>
        {
            DeviceId = MalformedPacketDeviceId,
            Timestamp = now,
            Sequence = 203,
            Payload = new PowerReading(400, PowerMetric.Watts)
        };
        Reject(store, TelemetryPacketValidator.Validate(mismatched, envDevice), mismatched.DeviceId);
    }

    private static void RejectEnvironmental(GatewayStore store, TelemetryPacket<EnvironmentalReading> packet)
    {
        var device = string.IsNullOrWhiteSpace(packet.DeviceId) ? null : store.FindSensor(packet.DeviceId);
        Reject(store, TelemetryPacketValidator.Validate(packet, device), packet.DeviceId);
    }

    private static void Reject(GatewayStore store, ValidationResult validation, string deviceId)
    {
        if (validation.IsValid)
        {
            throw new InvalidOperationException($"Malformed seed for '{deviceId}' was accepted; the validator should have refused it.");
        }

        store.RecordRejection(deviceId, validation.Errors);
    }

    public static bool DefersLiveStream(string deviceId)
    {
        if (string.Equals(deviceId, SilentSensorDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return LocationOutageDeviceIds.Contains(deviceId);
    }

    public const string LocationOutageNodeId = "node-feeder-1";

    public static readonly HashSet<string> LocationOutageDeviceIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "sx-pwr-feeder-a",
        "sx-pwr-feeder-b",
        "sx-act-feeder-switch"
    };

    /// <summary>
    /// Feeder node lost uplink: every device on that node last transmitted together,
    /// inside the stale window so the overview shows a location-level outage.
    /// </summary>
    public static void SeedLocationOutage(GatewayStore store)
    {
        var lastSampleAt = DateTimeOffset.UtcNow
            - TelemetryFreshnessClassifier.AgingWindow
            - TimeSpan.FromMinutes(3);
        var rng = new Random(7112);

        foreach (var deviceId in LocationOutageDeviceIds)
        {
            var device = store.FindSensor(deviceId)
                ?? throw new InvalidOperationException($"Location outage target '{deviceId}' is not registered.");

            if (!string.Equals(device.LocationNodeId, LocationOutageNodeId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"'{deviceId}' is not on {LocationOutageNodeId}.");
            }

            var ingest = store.GetIngestState(device.Id);
            if (ingest is { PacketCount: > 0 })
            {
                throw new InvalidOperationException($"'{device.Id}' already has packets; the outage stream would not be last-seen.");
            }

            TelemetryStreamSeeder.SeedHealthyStream(store, device, lastSampleAt, rng);
        }
    }

    // Climb back into the 18–26 °C crop band after the 44 °C spike.
    private static readonly float[] TemperatureRecoveryCelsius = [32f, 24f, 21.5f];

    /// <summary>
    /// Rack 1 temperature returns to a normal crop reading so operators can see
    /// recovery after Critical, without erasing the spike from history.
    /// </summary>
    public static void SeedRecovery(GatewayStore store)
    {
        var device = store.FindSensor(TemperatureSpikeDeviceId)
            ?? throw new InvalidOperationException($"Recovery target '{TemperatureSpikeDeviceId}' is not registered.");

        var ingest = store.GetIngestState(device.Id)
            ?? throw new InvalidOperationException($"No ingest state for '{device.Id}' — seed the spike first.");

        var timestamp = (ingest.LastIngestedAt ?? DateTimeOffset.UtcNow).AddSeconds(30);
        var sequence = ingest.LastSequence;

        foreach (var celsius in TemperatureRecoveryCelsius)
        {
            sequence++;
            timestamp = timestamp.AddSeconds(30);
            var packet = new TelemetryPacket<EnvironmentalReading>
            {
                DeviceId = device.Id,
                Timestamp = timestamp,
                Sequence = sequence,
                SignalQuality = 91,
                Payload = new EnvironmentalReading(celsius, EnvironmentalMetric.Temperature)
            };

            var validation = TelemetryPacketValidator.Validate(packet, device);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Recovery packet rejected for '{device.Id}' sequence {sequence}: {string.Join(" ", validation.Errors)}");
            }

            if (!store.TryIngestEnvironmental(packet, out var error))
            {
                throw new InvalidOperationException($"Recovery ingest failed for '{device.Id}': {error}");
            }
        }
    }
}
