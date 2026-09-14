namespace SmartX.Shared;

/// <summary>
/// Sensor class used when registering a device. Matches the assessment categories
/// so telemetry can be interpreted as environmental floats, power integers, or actuator booleans.
/// </summary>
public enum SensorCategory
{
    Environmental = 0,
    PowerConsumption = 1,
    Actuator = 2
}
