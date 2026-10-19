using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// LIFO history of manual overrides. Operators Undo by popping the last issued command
/// (e.g. revert a shut-all-valves action). Uses <see cref="Stack{T}"/>.
/// </summary>
public sealed class CommandUndoStack
{
    private readonly Stack<DeviceCommand> _stack = new();

    public int Depth => _stack.Count;

    public long TotalPushed { get; private set; }

    public long TotalPopped { get; private set; }

    public DateTimeOffset? LastPushedAt { get; private set; }

    public DateTimeOffset? LastPoppedAt { get; private set; }

    public void Push(DeviceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _stack.Push(command);
        TotalPushed++;
        LastPushedAt = command.IssuedAt == default ? DateTimeOffset.UtcNow : command.IssuedAt;
    }

    public bool TryPop(out DeviceCommand? command)
    {
        if (_stack.Count == 0)
        {
            command = null;
            return false;
        }

        command = _stack.Pop();
        TotalPopped++;
        LastPoppedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public bool TryPeek(out DeviceCommand? command)
    {
        if (_stack.Count == 0)
        {
            command = null;
            return false;
        }

        command = _stack.Peek();
        return true;
    }

    /// <summary>Newest-first snapshot for history UI (does not mutate the stack).</summary>
    public List<DeviceCommand> SnapshotNewestFirst() => [.. _stack];
}
