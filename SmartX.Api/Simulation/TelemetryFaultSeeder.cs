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
}
