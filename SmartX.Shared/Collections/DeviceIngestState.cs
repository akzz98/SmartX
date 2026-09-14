namespace SmartX.Shared;

/// <summary>
/// Per-device ingest counters tracked by SensorFleet (packet volume and last sequence).
/// </summary>
public sealed class DeviceIngestState
{
    public int PacketCount { get; set; }

    public int LastSequence { get; set; }

    public DateTimeOffset? LastIngestedAt { get; set; }
}
