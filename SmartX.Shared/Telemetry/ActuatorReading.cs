namespace SmartX.Shared;

/// <summary>
/// Boolean telemetry from an Actuator-category device (irrigation valve, smart switch).
/// Struct payload for TelemetryPacket&lt;ActuatorReading&gt; so the bool is not boxed.
/// IsActive means valve open / switch on, matching typical ESP32 GPIO flags.
/// </summary>
public readonly struct ActuatorReading
{
    public ActuatorReading(bool isActive, ActuatorKind kind)
    {
        IsActive = isActive;
        Kind = kind;
    }

    public bool IsActive { get; }

    public ActuatorKind Kind { get; }
}
