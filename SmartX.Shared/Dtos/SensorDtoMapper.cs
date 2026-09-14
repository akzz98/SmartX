namespace SmartX.Shared;

/// <summary>
/// Converts stored device identity into dashboard DTOs.
/// Health and freshness default until the gateway state engine in Stage 4 fills them in.
/// </summary>
public static class SensorDtoMapper
{
    public static SensorResponse ToResponse(
        SensorDevice device,
        HealthState health = HealthState.Normal,
        FreshnessState freshness = FreshnessState.Disconnected)
    {
        return new SensorResponse
        {
            Id = device.Id,
            MacAddress = device.MacAddress,
            LocationNodeId = device.LocationNodeId,
            Category = device.Category,
            RegisteredAt = device.RegisteredAt,
            LastSeenAt = device.LastSeenAt,
            Health = health,
            // No packets yet means the device has not been seen live on the mesh.
            Freshness = freshness
        };
    }

    public static SensorListResponse ToListResponse(IEnumerable<SensorDevice> devices)
    {
        var sensors = devices.Select(device => ToResponse(device)).ToList();
        return new SensorListResponse
        {
            Sensors = sensors,
            TotalCount = sensors.Count
        };
    }

    public static RegisterSensorResponse ToFailedRegistration(ValidationResult validation)
    {
        return new RegisterSensorResponse
        {
            Succeeded = false,
            Errors = [.. validation.Errors]
        };
    }

    public static RegisterSensorResponse ToSucceededRegistration(SensorDevice device)
    {
        return new RegisterSensorResponse
        {
            Succeeded = true,
            Sensor = ToResponse(device)
        };
    }
}
