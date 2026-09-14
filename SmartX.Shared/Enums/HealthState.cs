namespace SmartX.Shared;

/// <summary>
/// Health of a sensor after the gateway validates a packet or reading.
/// The dashboard shows this state; it does not detect it.
/// </summary>
public enum HealthState
{
    Normal = 0,
    Warning = 1,
    Critical = 2,
    Invalid = 3
}
