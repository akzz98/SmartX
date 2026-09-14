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
}
