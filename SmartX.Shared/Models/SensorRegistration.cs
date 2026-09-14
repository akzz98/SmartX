namespace SmartX.Shared;

/// <summary>
/// Fields an operator submits to attach a device to the gateway.
/// Validation of MAC, identity, and deployment placement is a separate step.
/// </summary>
public sealed class SensorRegistration
{
    public string? Id { get; set; }

    public string MacAddress { get; set; } = string.Empty;

    public string LocationNodeId { get; set; } = string.Empty;

    public SensorCategory Category { get; set; }
}
