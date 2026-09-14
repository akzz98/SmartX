using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Simulation;

/// <summary>
/// Publishes a healthy ESP32 stream for every seeded device so the gateway
/// has sequential history (arrays → List&lt;T&gt;) before later fault scenarios.
/// Values stay inside Normal health bands; last sample is recent so freshness is Live.
/// </summary>
public static class TelemetryStreamSeeder
{
    public const int SamplesPerDevice = 30;

    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(90);

    public static void SeedNormalStreams(GatewayStore store)
    {
        var now = DateTimeOffset.UtcNow;
        // Module code as seed: same demo stream on every restart, not crypto.
        var rng = new Random(7112);
        var devices = store.SnapshotSensors();
        if (devices.Count == 0)
        {
            throw new InvalidOperationException("Cannot seed telemetry before the mixed fleet is registered.");
        }

        foreach (var device in devices)
        {
            // Fault scenarios that own last-seen (dropout / location outage) skip the live stream.
            if (TelemetryFaultSeeder.DefersLiveStream(device.Id))
            {
                continue;
            }

            SeedHealthyStream(store, device, lastSampleAt: now, rng);
        }
    }

    /// <summary>
    /// Healthy samples ending at <paramref name="lastSampleAt"/>. A dropout scenario
    /// passes an old timestamp so freshness walks Live → Aging → Stale → Disconnected.
    /// </summary>
    public static void SeedHealthyStream(GatewayStore store, SensorDevice device, DateTimeOffset lastSampleAt, Random rng)
    {
        for (var sample = 0; sample < SamplesPerDevice; sample++)
        {
            var timestamp = lastSampleAt - (SampleInterval * (SamplesPerDevice - 1 - sample));
            var sequence = sample + 1;
            var quality = (byte)rng.Next(82, 97);

            var ingested = device.Category switch
            {
                SensorCategory.Environmental => IngestEnvironmental(store, device, timestamp, sequence, quality, rng),
                SensorCategory.PowerConsumption => IngestPower(store, device, timestamp, sequence, quality, rng),
                SensorCategory.Actuator => IngestActuator(store, device, timestamp, sequence, quality),
                _ => throw new InvalidOperationException($"No normal stream for category {device.Category}.")
            };

            if (!ingested)
            {
                throw new InvalidOperationException($"Stream ingest failed for '{device.Id}' sequence {sequence}.");
            }
        }
    }

    private static bool IngestEnvironmental(
        GatewayStore store,
        SensorDevice device,
        DateTimeOffset timestamp,
        int sequence,
        byte quality,
        Random rng)
    {
        var (metric, mean, amplitude) = EnvironmentalProfile(device.Id);
        var value = Jitter(rng, mean, amplitude);
        var packet = Packet(device.Id, timestamp, sequence, quality, new EnvironmentalReading(value, metric));
        EnsureValid(TelemetryPacketValidator.Validate(packet, device), device.Id, sequence);
        return store.TryIngestEnvironmental(packet, out _);
    }

    private static bool IngestPower(
        GatewayStore store,
        SensorDevice device,
        DateTimeOffset timestamp,
        int sequence,
        byte quality,
        Random rng)
    {
        var (mean, amplitude) = PowerProfile(device.Id);
        var watts = Jitter(rng, mean, amplitude);
        var packet = Packet(device.Id, timestamp, sequence, quality, new PowerReading(watts, PowerMetric.Watts));
        EnsureValid(TelemetryPacketValidator.Validate(packet, device), device.Id, sequence);
        return store.TryIngestPower(packet, out _);
    }

    private static bool IngestActuator(
        GatewayStore store,
        SensorDevice device,
        DateTimeOffset timestamp,
        int sequence,
        byte quality)
    {
        var kind = ActuatorKindFor(device.Id);
        var isActive = device.ExpectedIsActive ?? false;
        var packet = Packet(device.Id, timestamp, sequence, quality, new ActuatorReading(isActive, kind));
        EnsureValid(TelemetryPacketValidator.Validate(packet, device), device.Id, sequence);
        return store.TryIngestActuator(packet, out _);
    }

    private static TelemetryPacket<T> Packet<T>(
        string deviceId,
        DateTimeOffset timestamp,
        int sequence,
        byte quality,
        T payload)
        where T : struct
    {
        return new TelemetryPacket<T>
        {
            DeviceId = deviceId,
            Timestamp = timestamp,
            Sequence = sequence,
            SignalQuality = quality,
            Payload = payload
        };
    }

    private static (EnvironmentalMetric Metric, float Mean, float Amplitude) EnvironmentalProfile(string deviceId)
    {
        if (deviceId.Contains("moist", StringComparison.OrdinalIgnoreCase))
        {
            return (EnvironmentalMetric.Moisture, 55f, 6f);
        }

        if (deviceId.Contains("ph", StringComparison.OrdinalIgnoreCase))
        {
            return (EnvironmentalMetric.Ph, 6.0f, 0.18f);
        }

        if (deviceId.Contains("temp", StringComparison.OrdinalIgnoreCase))
        {
            return (EnvironmentalMetric.Temperature, 22.0f, 1.2f);
        }

        throw new InvalidOperationException($"No environmental stream profile for '{deviceId}'.");
    }

    private static (int Mean, int Amplitude) PowerProfile(string deviceId)
    {
        return deviceId switch
        {
            "sx-pwr-inverter" => (2100, 180),
            "sx-pwr-feeder-a" => (720, 70),
            "sx-pwr-feeder-b" => (640, 60),
            "sx-pwr-rack1" => (280, 35),
            _ => throw new InvalidOperationException($"No power stream profile for '{deviceId}'.")
        };
    }

    private static ActuatorKind ActuatorKindFor(string deviceId)
    {
        if (deviceId.Contains("valve", StringComparison.OrdinalIgnoreCase))
        {
            return ActuatorKind.Valve;
        }

        return ActuatorKind.Switch;
    }

    private static float Jitter(Random rng, float mean, float amplitude)
        => mean + ((float)rng.NextDouble() * 2f - 1f) * amplitude;

    private static int Jitter(Random rng, int mean, int amplitude)
        => mean + rng.Next(-amplitude, amplitude + 1);

    private static void EnsureValid(ValidationResult validation, string deviceId, int sequence)
    {
        if (validation.IsValid)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Normal stream packet rejected for '{deviceId}' sequence {sequence}: {string.Join(" ", validation.Errors)}");
    }
}
