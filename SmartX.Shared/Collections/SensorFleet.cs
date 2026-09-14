using System.Collections;

namespace SmartX.Shared;

/// <summary>
/// Custom fleet collection: ordered devices plus O(1) lookup by id/MAC and ingest counters.
/// List&lt;T&gt; alone would scan the whole fleet on every ingest; this tracks gateway state under load.
/// </summary>
public sealed class SensorFleet : IReadOnlyList<SensorDevice>
{
    private readonly List<SensorDevice> _order = [];
    private readonly Dictionary<string, SensorDevice> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SensorDevice> _byMac = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeviceIngestState> _ingest = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _order.Count;

    public SensorDevice this[int index] => _order[index];

    public IEnumerator<SensorDevice> GetEnumerator() => _order.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool TryAdd(SensorDevice device, out string? error)
    {
        if (_byMac.ContainsKey(device.MacAddress))
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
        _byMac[device.MacAddress] = device;
        _ingest[device.Id] = new DeviceIngestState();
        error = null;
        return true;
    }

    public SensorDevice? FindById(string id)
        => _byId.TryGetValue(id, out var device) ? device : null;

    public SensorDevice? FindByMac(string mac)
        => _byMac.TryGetValue(mac, out var device) ? device : null;

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
