using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// Timestamp-ordered history for the command-stream timeline.
/// Uses <see cref="SortedDictionary{TKey, TValue}"/> so entries enumerate oldest→newest
/// without a separate sort pass when the dashboard renders graphs.
/// </summary>
public sealed class TimelineLog
{
    // Unique key per entry: ticks + sequence so two events at the same instant stay ordered.
    private readonly SortedDictionary<long, TimelineEntry> _byKey = new();
    private long _sequence;

    public int Count => _byKey.Count;

    public void AppendCommand(DeviceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Append(new TimelineEntry
        {
            Kind = TimelineEntryKind.Command,
            Timestamp = command.IssuedAt == default ? DateTimeOffset.UtcNow : command.IssuedAt,
            DeviceId = command.DeviceId,
            MacAddress = command.MacAddress,
            Summary = $"{command.Kind} → {(command.TargetIsActive ? "active" : "inactive")}",
            Command = command
        });
    }

    public void AppendAlert(PriorityAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        Append(new TimelineEntry
        {
            Kind = TimelineEntryKind.Alert,
            Timestamp = alert.RaisedAt == default ? DateTimeOffset.UtcNow : alert.RaisedAt,
            DeviceId = alert.DeviceId,
            MacAddress = alert.MacAddress,
            Summary = $"{alert.Severity}: {alert.Reason}",
            Alert = alert
        });
    }

    public IReadOnlyList<TimelineEntry> SnapshotOldestFirst() => [.. _byKey.Values];

    public IReadOnlyList<TimelineEntry> SnapshotNewestFirst()
    {
        var list = new List<TimelineEntry>(_byKey.Count);
        foreach (var entry in _byKey.Values.Reverse())
        {
            list.Add(entry);
        }

        return list;
    }

    private void Append(TimelineEntry entry)
    {
        _sequence++;
        var ticks = entry.Timestamp.UtcTicks;
        var key = ticks + _sequence;
        // Keep keys unique and still ordered by time: ticks dominate, sequence breaks ties.
        while (_byKey.ContainsKey(key))
        {
            key++;
        }

        _byKey[key] = entry;
    }
}

public enum TimelineEntryKind
{
    Command = 0,
    Alert = 1
}

public sealed class TimelineEntry
{
    public TimelineEntryKind Kind { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public string DeviceId { get; init; } = string.Empty;

    public string MacAddress { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public DeviceCommand? Command { get; init; }

    public PriorityAlert? Alert { get; init; }
}
