namespace SmartX.Shared;

/// <summary>
/// One manual override issued to a registered device. The Part 2 command stack stores these
/// so operators can Undo the last action (e.g. revert a shut-all-valves burst).
/// </summary>
public sealed class DeviceCommand
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Registered sensor / node id the override targets.</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Hardware MAC at issue time — supports O(1) dictionary lookup on the live fleet.</summary>
    public string MacAddress { get; set; } = string.Empty;

    public CommandKind Kind { get; set; }

    /// <summary>Desired actuator position after the override (open/on = true).</summary>
    public bool TargetIsActive { get; set; }

    /// <summary>Actuator position before this override, so Undo can restore it.</summary>
    public bool? PreviousIsActive { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public string? OperatorNote { get; set; }
}
