namespace SmartX.Shared;

/// <summary>
/// What an operator attached to an ESP32 profile: rack photo, JSON config, or hardware log.
/// </summary>
public enum AttachmentKind
{
    Configuration = 0,
    Photo = 1,
    HardwareLog = 2
}
