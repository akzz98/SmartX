using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Simulation;

/// <summary>
/// Builds a realistic undirected mesh for the seeded fleet: gateway + ESP32 vertices,
/// multi-neighbour wireless edges with power/RSSI-style costs (Final PoE Stage 4.2).
/// </summary>
public static class MeshTopologySeeder
{
    public static void Seed(GatewayStore store)
    {
        var graph = store.Mesh;
        graph.Clear();

        graph.AddNode(new MeshNode
        {
            Id = MeshGraph.GatewayNodeId,
            Kind = MeshNodeKind.Gateway,
            MacAddress = string.Empty,
            LocationNodeId = "site-root",
            IsOnline = true,
            Freshness = FreshnessState.Live,
            Health = HealthState.Normal
        });

        foreach (var sensor in store.SnapshotSensors())
        {
            graph.AddNode(ToMeshNode(sensor));
        }

        // Gateway uplinks — strong, low power cost.
        Link(graph, MeshGraph.GatewayNodeId, "sx-pwr-inverter", 1.0, "gateway ↔ inverter");
        Link(graph, MeshGraph.GatewayNodeId, "sx-pwr-rack1", 1.2, "gateway ↔ rack1 power");
        Link(graph, MeshGraph.GatewayNodeId, "sx-env-sump-temp", 1.5, "gateway ↔ sump probe");

        // Rack 1 local mesh (multi-neighbour).
        Link(graph, "sx-pwr-rack1", "sx-env-rack1-temp", 2.0, "rack1 power ↔ temp");
        Link(graph, "sx-env-rack1-temp", "sx-env-rack1-moist", 1.1, "rack1 temp ↔ moist");
        Link(graph, "sx-env-rack1-moist", "sx-env-rack1-ph", 1.1, "rack1 moist ↔ ph");
        Link(graph, "sx-env-rack1-ph", "sx-act-rack1-valve", 1.3, "rack1 ph ↔ valve");
        Link(graph, "sx-pwr-rack1", "sx-act-rack1-valve", 1.4, "rack1 power ↔ valve");
        Link(graph, "sx-env-rack1-temp", "sx-act-rack1-valve", 1.8, "rack1 temp ↔ valve");

        // Rack 2 local mesh.
        Link(graph, "sx-env-rack2-temp", "sx-env-rack2-moist", 1.1, "rack2 temp ↔ moist");
        Link(graph, "sx-env-rack2-moist", "sx-act-rack2-valve", 1.3, "rack2 moist ↔ valve");
        Link(graph, "sx-env-rack2-temp", "sx-act-rack2-valve", 1.6, "rack2 temp ↔ valve");

        // Rack 3 local mesh.
        Link(graph, "sx-env-rack3-temp", "sx-env-rack3-ph", 1.2, "rack3 temp ↔ ph");

        // Sump / pump cluster.
        Link(graph, "sx-env-sump-temp", "sx-act-sump-pump", 1.2, "sump temp ↔ pump");
        Link(graph, "sx-act-sump-pump", "sx-env-rack1-moist", 2.4, "pump ↔ rack1 moist (irrigation)");

        // Feeder bay cluster (multi-neighbour).
        Link(graph, "sx-pwr-feeder-a", "sx-pwr-feeder-b", 1.0, "feeder A ↔ B");
        Link(graph, "sx-pwr-feeder-a", "sx-act-feeder-switch", 1.2, "feeder A ↔ switch");
        Link(graph, "sx-pwr-feeder-b", "sx-act-feeder-switch", 1.2, "feeder B ↔ switch");
        Link(graph, "sx-pwr-inverter", "sx-pwr-feeder-a", 2.0, "inverter ↔ feeder A");

        // Inter-rack / site backbone (redundant hops for later BFS healing + MST).
        Link(graph, "sx-env-rack1-temp", "sx-env-rack2-temp", 2.5, "rack1 ↔ rack2 backbone");
        Link(graph, "sx-env-rack2-temp", "sx-env-rack3-temp", 2.6, "rack2 ↔ rack3 backbone");
        Link(graph, "sx-env-rack1-moist", "sx-env-rack2-moist", 2.8, "rack1 ↔ rack2 moist");
        Link(graph, "sx-act-rack2-valve", "sx-act-sump-pump", 3.0, "rack2 valve ↔ sump pump");
        Link(graph, "sx-pwr-rack1", "sx-pwr-inverter", 1.7, "rack1 power ↔ inverter");
        Link(graph, "sx-env-rack3-ph", "sx-pwr-feeder-b", 3.5, "rack3 ↔ feeder (weak hop)");

        if (graph.NodeCount != store.SnapshotSensors().Count + 1)
        {
            throw new InvalidOperationException(
                $"Mesh node count {graph.NodeCount} should be fleet size + gateway.");
        }

        if (graph.EdgeCount < 20)
        {
            throw new InvalidOperationException(
                $"Expected a multi-neighbour mesh (≥ 20 edges); was {graph.EdgeCount}.");
        }

        // Every sensor should have at least one wireless neighbour.
        foreach (var sensor in store.SnapshotSensors())
        {
            if (graph.Neighbours(sensor.Id).Count == 0)
            {
                throw new InvalidOperationException($"Sensor '{sensor.Id}' has no mesh neighbours.");
            }
        }
    }

    public static void RefreshOnlineStatus(GatewayStore store)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var sensor in store.SnapshotSensors())
        {
            // Reclassify from LastSeenAt so forced mesh-down ages stay sticky on API reads.
            sensor.Freshness = TelemetryFreshnessClassifier.Classify(sensor.LastSeenAt, now);

            if (!store.Mesh.TryGetNode(sensor.Id, out var node) || node is null)
            {
                continue;
            }

            node.Freshness = sensor.Freshness;
            node.Health = sensor.Health;
            node.IsOnline = sensor.Freshness is not FreshnessState.Disconnected;
        }

        if (store.Mesh.TryGetNode(MeshGraph.GatewayNodeId, out var gateway) && gateway is not null)
        {
            gateway.IsOnline = true;
            gateway.Freshness = FreshnessState.Live;
            gateway.Health = HealthState.Normal;
        }
    }

    private static MeshNode ToMeshNode(SensorDevice sensor)
        => new()
        {
            Id = sensor.Id,
            Kind = MeshNodeKind.Sensor,
            MacAddress = sensor.MacAddress,
            LocationNodeId = sensor.LocationNodeId,
            Category = sensor.Category,
            Freshness = sensor.Freshness,
            Health = sensor.Health,
            IsOnline = sensor.Freshness is not FreshnessState.Disconnected
        };

    private static void Link(MeshGraph graph, string from, string to, double weight, string label)
    {
        graph.AddUndirectedEdge(new MeshEdge
        {
            FromId = from,
            ToId = to,
            Weight = weight,
            LinkKind = MeshLinkKind.PowerCost,
            Label = label
        });
    }
}
