namespace SmartX.Shared;

/// <summary>
/// Manual override an operator can push to an actuator-class ESP32.
/// Maps onto valve / pump / relay devices already on the fleet — not a generic CRUD action.
/// </summary>
public enum CommandKind
{
    OpenValve = 0,
    CloseValve = 1,
    StartPump = 2,
    StopPump = 3,
    ToggleRelay = 4
}
