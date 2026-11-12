using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Simulation;

/// <summary>
/// Forces a mid-mesh relay offline so BFS healing / alternate hops are visible
/// on <c>GET /api/mesh/traversal</c> (Final PoE Stage 5.4).
/// </summary>
public static class MeshDownScenarioSeeder
{
    /// <summary>
    /// Power inverter — gateway uplink hub with redundant paths via rack1 power
    /// and the rack3 ↔ feeder backbone, so a down status still yields a next hop.
    /// </summary>
    public const string DownRelayDeviceId = "sx-pwr-inverter";

    public static void Seed(GatewayStore store)
    {
        var device = store.FindSensor(DownRelayDeviceId)
            ?? throw new InvalidOperationException(
                $"Mesh-down target '{DownRelayDeviceId}' is not registered.");

        // Past StaleWindow so freshness is Disconnected immediately (not merely Stale).
        device.LastSeenAt = DateTimeOffset.UtcNow
            - TelemetryFreshnessClassifier.StaleWindow
            - TimeSpan.FromMinutes(10);
        device.Freshness = FreshnessState.Disconnected;
        device.Health = HealthState.Critical;

        MeshTopologySeeder.RefreshOnlineStatus(store);

        if (!store.Mesh.TryGetNode(DownRelayDeviceId, out var node) ||
            node is null ||
            node.IsOnline)
        {
            throw new InvalidOperationException(
                $"Mesh-down seed failed: '{DownRelayDeviceId}' should be offline in the graph.");
        }

        // Priority alert so the command stream also shows the mesh fault.
        store.RaisePriorityAlert(new PriorityAlert
        {
            DeviceId = device.Id,
            MacAddress = device.MacAddress,
            Severity = AlertSeverity.Warning,
            Reason =
                "Mesh relay down — inverter offline; BFS suggests alternate hops via live neighbours",
            RaisedAt = DateTimeOffset.UtcNow
        });
    }
}
