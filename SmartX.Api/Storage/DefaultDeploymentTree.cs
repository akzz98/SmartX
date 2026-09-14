using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// Starter site → zone → sub-zone → node forest so registration can check LocationNodeId
/// before Stage 6 seeds a larger simulated fleet. Node ids are what operators submit.
/// </summary>
public static class DefaultDeploymentTree
{
    public static List<DeploymentNode> Create()
    {
        return
        [
            new DeploymentNode
            {
                Id = "site-hydro-a",
                Name = "Hydroponic Facility A",
                Level = DeploymentLevel.Site,
                Children =
                [
                    new DeploymentNode
                    {
                        Id = "zone-1",
                        Name = "Zone 1",
                        Level = DeploymentLevel.Zone,
                        Children =
                        [
                            new DeploymentNode
                            {
                                Id = "sub-zone-b",
                                Name = "Sub-Zone B",
                                Level = DeploymentLevel.SubZone,
                                Children =
                                [
                                    new DeploymentNode
                                    {
                                        Id = "node-rack-1",
                                        Name = "Rack 1",
                                        Level = DeploymentLevel.Node
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        ];
    }
}
