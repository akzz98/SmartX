namespace SmartX.Shared;

/// <summary>
/// One issued override as returned to the command-stream UI (history row or issue/undo result).
/// </summary>
public sealed class CommandResponse
{
    public string Id { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string MacAddress { get; set; } = string.Empty;

    public CommandKind Kind { get; set; }

    public bool TargetIsActive { get; set; }

    public bool? PreviousIsActive { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public string? OperatorNote { get; set; }
}
