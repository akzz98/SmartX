namespace SmartX.Api.Storage;

/// <summary>
/// Tracks currently active unique disconnect / error keys with a <see cref="HashSet{T}"/>.
/// Duplicate alerts for the same sensor key are ignored so the gateway does not spam
/// the command stream (Part 2 Sets rubric).
/// </summary>
public sealed class UniqueAlertSet
{
    private readonly HashSet<string> _active = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _active.Count;

    /// <summary>
    /// Tries to register an active alert key. Returns false when the key is already present
    /// (duplicate disconnect/error for the same sensor).
    /// </summary>
    public bool TryAdd(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return _active.Add(key.Trim());
    }

    public bool Contains(string key)
        => !string.IsNullOrWhiteSpace(key) && _active.Contains(key.Trim());

    /// <summary>Clears a key when the node recovers so a later disconnect can alert again.</summary>
    public bool TryRemove(string key)
        => !string.IsNullOrWhiteSpace(key) && _active.Remove(key.Trim());

    public IReadOnlyCollection<string> Snapshot() => _active.ToArray();

    public static string DisconnectKey(string deviceId)
        => $"disconnect:{deviceId.Trim()}";

    public static string ErrorKey(string deviceId, string reason)
        => $"error:{deviceId.Trim()}:{reason.Trim()}";
}
