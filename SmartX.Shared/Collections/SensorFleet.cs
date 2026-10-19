using System.Collections;

namespace SmartX.Shared;

/// <summary>
/// Live registered-device cache: ordered devices plus O(1) <see cref="Dictionary{TKey, TValue}"/>
/// lookups by unique id and MAC. Part 2 command stream and telemetry ingest resolve devices
/// here instead of scanning nested loops under load.
/// </summary>
public sealed class SensorFleet : IReadOnlyList<SensorDevice>
{
    private readonly List<SensorDevice> _order = [];
    private readonly Dictionary<string, SensorDevice> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SensorDevice> _byMac = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeviceIngestState> _ingest = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many devices are keyed in the live id dictionary (same as Count).</summary>
    public int LiveLookupCount => _byId.Count;

    public int Count => _order.Count;

    public SensorDevice this[int index] => _order[index];

    public IEnumerator<SensorDevice> GetEnumerator() => _order.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool TryAdd(SensorDevice device, out string? error)
    {
        var macKey = NormalizeMac(device.MacAddress);
        if (_byMac.ContainsKey(macKey))
        {
            error = "A sensor with this MAC address is already registered.";
            return false;
        }

        if (_byId.ContainsKey(device.Id))
        {
            error = "A sensor with this device id is already registered.";
            return false;
        }

        _order.Add(device);
        _byId[device.Id] = device;
        _byMac[macKey] = device;
        _ingest[device.Id] = new DeviceIngestState();
        error = null;
        return true;
    }

    public SensorDevice? FindById(string id)
        => string.IsNullOrWhiteSpace(id)
            ? null
            : _byId.TryGetValue(id.Trim(), out var device) ? device : null;

    public SensorDevice? FindByMac(string mac)
    {
        if (string.IsNullOrWhiteSpace(mac))
        {
            return null;
        }

        var key = NormalizeMac(mac);
        return _byMac.TryGetValue(key, out var device) ? device : null;
    }

    /// <summary>
    /// O(1) live resolve for ingest/commands: try unique device id, then MAC address.
    /// Does not scan the ordered list.
    /// </summary>
    public SensorDevice? ResolveLive(string deviceIdOrMac)
    {
        if (string.IsNullOrWhiteSpace(deviceIdOrMac))
        {
            return null;
        }

        var key = deviceIdOrMac.Trim();
        if (_byId.TryGetValue(key, out var byId))
        {
            return byId;
        }

        return FindByMac(key);
    }

    private static string NormalizeMac(string mac)
        => mac.Trim().Replace('-', ':').ToUpperInvariant();

    public DeviceIngestState? GetIngestState(string id)
        => _ingest.TryGetValue(id, out var state) ? state : null;

    public void RecordIngest(string deviceId, int sequence, DateTimeOffset timestamp)
    {
        if (!_ingest.TryGetValue(deviceId, out var state))
        {
            return;
        }

        state.PacketCount++;
        state.LastSequence = sequence;
        state.LastIngestedAt = timestamp;
    }

    public List<SensorDevice> Snapshot() => [.. _order];
}
