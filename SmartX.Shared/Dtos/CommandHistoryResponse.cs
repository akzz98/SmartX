namespace SmartX.Shared;

/// <summary>
/// Manual override history for the command stream (stack order or chronological — decided by the API).
/// </summary>
public sealed class CommandHistoryResponse
{
    public List<CommandResponse> Commands { get; set; } = [];

    /// <summary>How many overrides can still be undone (stack depth).</summary>
    public int UndoStackDepth { get; set; }
}
