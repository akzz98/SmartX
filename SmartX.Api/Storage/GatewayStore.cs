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
}
