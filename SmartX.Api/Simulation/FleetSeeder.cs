using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Simulation;

/// <summary>
/// Registers a mixed ESP32 fleet across hydroponic racks and utility nodes.
/// Packets are seeded afterwards by TelemetryStreamSeeder (normal first, then faults).
/// </summary>
public static class FleetSeeder
{
    public static void SeedDevices(GatewayStore store)
    {
        var now = DateTimeOffset.UtcNow;
        SensorDevice[] devices =
        [
            Device("sx-env-rack1-temp", "AA:BB:CC:10:01:01", "node-rack-1", SensorCategory.Environmental, now),
            Device("sx-env-rack1-moist", "AA:BB:CC:10:01:02", "node-rack-1", SensorCategory.Environmental, now),
            Device("sx-env-rack1-ph", "AA:BB:CC:10:01:03", "node-rack-1", SensorCategory.Environmental, now),
            Device("sx-env-rack2-temp", "AA:BB:CC:10:02:01", "node-rack-2", SensorCategory.Environmental, now),
            Device("sx-env-rack2-moist", "AA:BB:CC:10:02:02", "node-rack-2", SensorCategory.Environmental, now),
            Device("sx-env-rack3-temp", "AA:BB:CC:10:03:01", "node-rack-3", SensorCategory.Environmental, now),
            Device("sx-env-rack3-ph", "AA:BB:CC:10:03:02", "node-rack-3", SensorCategory.Environmental, now),
            Device("sx-env-sump-temp", "AA:BB:CC:10:04:01", "node-sump-1", SensorCategory.Environmental, now),
            Device("sx-pwr-inverter", "AA:BB:CC:20:01:01", "node-inverter-1", SensorCategory.PowerConsumption, now),
            Device("sx-pwr-feeder-a", "AA:BB:CC:20:02:01", "node-feeder-1", SensorCategory.PowerConsumption, now),
            Device("sx-pwr-feeder-b", "AA:BB:CC:20:02:02", "node-feeder-1", SensorCategory.PowerConsumption, now),
            Device("sx-pwr-rack1", "AA:BB:CC:20:03:01", "node-rack-1", SensorCategory.PowerConsumption, now),
            Device("sx-act-rack1-valve", "AA:BB:CC:30:01:01", "node-rack-1", SensorCategory.Actuator, now, expectedIsActive: true),
            Device("sx-act-rack2-valve", "AA:BB:CC:30:02:01", "node-rack-2", SensorCategory.Actuator, now, expectedIsActive: true),
            Device("sx-act-sump-pump", "AA:BB:CC:30:04:01", "node-sump-1", SensorCategory.Actuator, now, expectedIsActive: false),
            Device("sx-act-feeder-switch", "AA:BB:CC:30:05:01", "node-feeder-1", SensorCategory.Actuator, now, expectedIsActive: true)
        ];

        foreach (var device in devices)
        {
            var registration = new SensorRegistration
            {
                Id = device.Id,
                MacAddress = device.MacAddress,
                LocationNodeId = device.LocationNodeId,
                Category = device.Category
            };
            var validation = SensorRegistrationValidator.Validate(registration, store.DeploymentRoots);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Fleet seed rejected '{device.Id}': {string.Join(" ", validation.Errors)}");
            }

            if (!store.TryAdd(device, out var error))
            {
                throw new InvalidOperationException($"Fleet seed failed '{device.Id}': {error}");
            }
        }
    }

    private static SensorDevice Device(
        string id,
        string mac,
        string locationNodeId,
        SensorCategory category,
        DateTimeOffset registeredAt,
        bool? expectedIsActive = null)
    {
        return new SensorDevice
        {
            Id = id,
            MacAddress = mac,
            LocationNodeId = locationNodeId,
            Category = category,
            RegisteredAt = registeredAt,
            LastSeenAt = null,
            Health = HealthState.Normal,
            Freshness = FreshnessState.Disconnected,
            ExpectedIsActive = expectedIsActive
        };
    }
}
