namespace SmartX.Shared;

/// <summary>
/// Uses EnvironmentalReading operator - so the detail view can show a spike size, not only an absolute value.
/// </summary>
public static class EnvironmentalDelta
{
    public static EnvironmentalReading? Latest(IEnumerable<TelemetryPacket<EnvironmentalReading>> packets)
    {
        var latestTwo = packets
            .OrderByDescending(packet => packet.Timestamp)
            .Take(2)
            .ToList();

        if (latestTwo.Count < 2 || latestTwo[0].Payload.Metric != latestTwo[1].Payload.Metric)
        {
            return null;
        }

        return latestTwo[0].Payload - latestTwo[1].Payload;
    }
}
