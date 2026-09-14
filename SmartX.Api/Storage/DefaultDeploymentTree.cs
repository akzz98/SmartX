using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// Simulated South African hydroponic + utility sites. Every path is Site → Zone → SubZone → Node
/// so recursive validation accepts the forest before devices are seeded.
/// </summary>
public static class DefaultDeploymentTree
{
    public static List<DeploymentNode> Create()
    {
        return
        [
            Node("site-hydro-a", "Hydroponic Facility A", DeploymentLevel.Site,
            [
                Node("zone-1", "Zone 1", DeploymentLevel.Zone,
                [
                    Node("sub-zone-b", "Sub-Zone B", DeploymentLevel.SubZone,
                    [
                        Node("node-rack-1", "Rack 1", DeploymentLevel.Node),
                        Node("node-rack-2", "Rack 2", DeploymentLevel.Node)
                    ])
                ]),
                Node("zone-2", "Zone 2", DeploymentLevel.Zone,
                [
                    Node("sub-zone-c", "Sub-Zone C", DeploymentLevel.SubZone,
                    [
                        Node("node-rack-3", "Rack 3", DeploymentLevel.Node),
                        Node("node-sump-1", "Sump 1", DeploymentLevel.Node)
                    ])
                ])
            ]),
            Node("site-grid-b", "Utility Yard B", DeploymentLevel.Site,
            [
                Node("zone-grid", "Grid Zone", DeploymentLevel.Zone,
                [
                    Node("sub-zone-meters", "Meter Bank", DeploymentLevel.SubZone,
                    [
                        Node("node-inverter-1", "Inverter 1", DeploymentLevel.Node),
                        Node("node-feeder-1", "Feeder 1", DeploymentLevel.Node)
                    ])
                ])
            ])
        ];
    }

    private static DeploymentNode Node(
        string id,
        string name,
        DeploymentLevel level,
        List<DeploymentNode>? children = null)
    {
        return new DeploymentNode
        {
            Id = id,
            Name = name,
            Level = level,
            Children = children ?? []
        };
    }
}
