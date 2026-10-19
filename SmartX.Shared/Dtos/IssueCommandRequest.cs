namespace SmartX.Shared;

/// <summary>
/// POST body for a manual actuator override on the command stream.
/// </summary>
public sealed class IssueCommandRequest
{
    public string DeviceId { get; set; } = string.Empty;

    public CommandKind Kind { get; set; }

    public string? OperatorNote { get; set; }
}
