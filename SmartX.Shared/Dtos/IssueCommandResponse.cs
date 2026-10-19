namespace SmartX.Shared;

/// <summary>
/// Result of issuing a manual override. On success the command has been pushed onto the undo stack.
/// </summary>
public sealed class IssueCommandResponse
{
    public bool Succeeded { get; set; }

    public CommandResponse? Command { get; set; }

    public List<string> Errors { get; set; } = [];
}
