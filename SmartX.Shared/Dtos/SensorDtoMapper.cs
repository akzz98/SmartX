namespace SmartX.Shared;

/// <summary>
/// Converts stored device identity into dashboard DTOs.
/// Health and freshness are copied from the gateway record, not invented in the UI.
/// </summary>
public static class SensorDtoMapper
{
    public static SensorResponse ToResponse(SensorDevice device)
    {
        return new SensorResponse
        {
            Id = device.Id,
            MacAddress = device.MacAddress,
            LocationNodeId = device.LocationNodeId,
            Category = device.Category,
            RegisteredAt = device.RegisteredAt,
            LastSeenAt = device.LastSeenAt,
            Health = device.Health,
            Freshness = device.Freshness
        };
    }

    public static SensorListResponse ToListResponse(IEnumerable<SensorDevice> devices)
    {
        var sensors = devices.Select(ToResponse).ToList();
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
