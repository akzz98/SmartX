namespace SmartX.Shared;

/// <summary>
/// Uses PowerReading operator + to fold the latest watts from each meter into one site load.
/// This is the assessment aggregation example: Meter3 = Meter1 + Meter2, applied to the live fleet.
/// </summary>
public static class PowerLoadAggregator
{
    public static PowerReading CombinedLatestWatts(IEnumerable<TelemetryPacket<PowerReading>> packets)
    {
        var latestPerMeter = packets
            .Where(packet => packet.Payload.Metric == PowerMetric.Watts)
            .GroupBy(packet => packet.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(packet => packet.Timestamp).First().Payload);

        PowerReading? total = null;
        foreach (var reading in latestPerMeter)
        {
            total = total is null ? reading : total.Value + reading;
        }

        return total ?? new PowerReading(0, PowerMetric.Watts);
    }

    public static PowerReading? LatestWattDelta(IEnumerable<TelemetryPacket<PowerReading>> packets)
    {
        var latestTwo = packets
            .Where(packet => packet.Payload.Metric == PowerMetric.Watts)
            .OrderByDescending(packet => packet.Timestamp)
            .Take(2)
            .Select(packet => packet.Payload)
            .ToList();

        if (latestTwo.Count < 2)
        {
            return null;
        }

        return latestTwo[0] - latestTwo[1];
    }
}
