namespace SmartX.Shared;

/// <summary>
/// Nested placement of devices (for example Facility A → Zone 1 → Sub-Zone B → Node).
/// Children make the hierarchy walkable; the gateway will validate this tree recursively.
/// </summary>
public sealed class DeploymentNode
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DeploymentLevel Level { get; set; }

    public List<DeploymentNode> Children { get; set; } = [];
}
