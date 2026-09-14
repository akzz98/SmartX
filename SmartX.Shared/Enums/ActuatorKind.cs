namespace SmartX.Shared;

/// <summary>
/// Which Boolean actuator published the packet. Valves and switches are both bools,
/// but a stuck valve is a different fault story from a stuck relay.
/// </summary>
public enum ActuatorKind
{
    Valve = 0,
    Switch = 1
}
