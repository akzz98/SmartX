namespace SmartX.Shared;

/// <summary>
/// Depth of a node in the deployment tree. Recursion later checks that a sensor
/// is attached under a valid site → zone → sub-zone → node path.
/// </summary>
public enum DeploymentLevel
{
    Site = 0,
    Zone = 1,
    SubZone = 2,
    Node = 3
}
