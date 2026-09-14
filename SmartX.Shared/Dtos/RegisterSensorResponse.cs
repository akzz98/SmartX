namespace SmartX.Shared;

/// <summary>
/// Result of a registration attempt. Errors come from SensorRegistrationValidator,
/// not from the dashboard inventing problems.
/// </summary>
public sealed class RegisterSensorResponse
{
    public bool Succeeded { get; set; }

    public SensorResponse? Sensor { get; set; }

    public List<string> Errors { get; set; } = [];
}
