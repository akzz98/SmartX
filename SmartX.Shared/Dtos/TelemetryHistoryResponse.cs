namespace SmartX.Shared;

/// <summary>
/// Progressive Disclosure level 3: typed history for one device.
/// Only the list that matches Category is filled — still no object/boxing.
/// </summary>
public sealed class TelemetryHistoryResponse
{
    public string DeviceId { get; set; } = string.Empty;

    public SensorCategory Category { get; set; }

    public List<TelemetryPacket<EnvironmentalReading>> Environmental { get; set; } = [];

    public List<TelemetryPacket<PowerReading>> Power { get; set; } = [];

    public List<TelemetryPacket<ActuatorReading>> Actuator { get; set; } = [];
}
