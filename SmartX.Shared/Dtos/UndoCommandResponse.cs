namespace SmartX.Shared;

/// <summary>
/// Result of popping the last override from the command stack and restoring previous actuator state.
/// </summary>
public sealed class UndoCommandResponse
{
    public bool Succeeded { get; set; }

    /// <summary>The override that was undone; null when the stack was empty or undo failed.</summary>
    public CommandResponse? UndoneCommand { get; set; }

    public List<string> Errors { get; set; } = [];
}
