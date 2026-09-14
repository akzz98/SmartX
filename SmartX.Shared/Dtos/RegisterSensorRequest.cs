namespace SmartX.Shared;

/// <summary>
/// POST body for registering a device with the gateway.
/// Kept as a DTO so the HTTP contract can stay stable if SensorDevice grows extra stored fields.
/// </summary>
public sealed class RegisterSensorRequest
{
    public string? Id { get; set; }

    public string MacAddress { get; set; } = string.Empty;

    public string LocationNodeId { get; set; } = string.Empty;

    public SensorCategory Category { get; set; }

    /// <summary>
    /// Maps the HTTP payload onto the domain type the validator already understands.
    /// </summary>
    public SensorRegistration ToRegistration()
    {
        return new SensorRegistration
        {
            Id = Id,
            MacAddress = MacAddress,
            LocationNodeId = LocationNodeId,
            Category = Category
        };
    }
}
