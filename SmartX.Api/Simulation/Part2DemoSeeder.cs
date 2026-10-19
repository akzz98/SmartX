using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Simulation;

/// <summary>
/// Part 2 demo seeds that prove queues, priority bypass, HashSet dedupe, and related behaviours.
/// </summary>
public static class Part2DemoSeeder
{
    public const string FifoBurstDeviceId = "sx-env-rack2-temp";

    public const int FifoBurstPacketCount = 12;

    /// <summary>
    /// Parks a burst of normal packets on the standard FIFO queue (no drain) so depth
    /// climbs under load, then drains them into typed history. PeakDepth stays as evidence.
    /// </summary>
    public static void SeedFifoBurst(GatewayStore store)
    {
        var device = store.FindSensor(FifoBurstDeviceId)
            ?? throw new InvalidOperationException($"FIFO burst target '{FifoBurstDeviceId}' is not registered.");

        var ingest = store.GetIngestState(device.Id)
            ?? throw new InvalidOperationException($"No ingest state for '{device.Id}' — seed the normal stream first.");

        var timestamp = (ingest.LastIngestedAt ?? DateTimeOffset.UtcNow).AddSeconds(5);
        var sequence = ingest.LastSequence;
        var beforeEnqueued = store.StandardIngestQueue.TotalEnqueued;

        for (var i = 0; i < FifoBurstPacketCount; i++)
        {
            sequence++;
            timestamp = timestamp.AddSeconds(2);
            var packet = new TelemetryPacket<EnvironmentalReading>
            {
                DeviceId = device.Id,
                Timestamp = timestamp,
                Sequence = sequence,
                SignalQuality = 92,
                Payload = new EnvironmentalReading(21.5f + (i * 0.05f), EnvironmentalMetric.Temperature)
            };

            var validation = TelemetryPacketValidator.Validate(packet, device);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"FIFO burst packet rejected for '{device.Id}': {string.Join(" ", validation.Errors)}");
            }

            store.EnqueueStandardWithoutDrain(QueuedTelemetryItem.FromEnvironmental(packet));
        }

        if (store.StandardIngestQueue.Depth < FifoBurstPacketCount)
        {
            throw new InvalidOperationException(
                $"Expected FIFO depth >= {FifoBurstPacketCount} during burst; was {store.StandardIngestQueue.Depth}.");
        }

        if (store.StandardIngestQueue.PeakDepth < FifoBurstPacketCount)
        {
            throw new InvalidOperationException(
                $"Expected FIFO PeakDepth >= {FifoBurstPacketCount}; was {store.StandardIngestQueue.PeakDepth}.");
        }

        if (store.StandardIngestQueue.TotalEnqueued < beforeEnqueued + FifoBurstPacketCount)
        {
            throw new InvalidOperationException("FIFO burst did not increase TotalEnqueued as expected.");
        }

        // Drain so the rest of the gateway sees a consistent fleet (depth returns to 0).
        store.DrainPendingQueues();

        if (store.StandardIngestQueue.Depth != 0)
        {
            throw new InvalidOperationException("FIFO queue should be empty after the burst drain.");
        }
    }

    public const string CriticalPowerDeviceId = "sx-pwr-inverter";

    /// <summary>
    /// Parks a normal power packet on FIFO, then raises a Critical power-spike alert on the
    /// priority path so it bypasses the backlog (pairs with the moisture bypass seed).
    /// </summary>
    public static void SeedCriticalPowerPriorityBypass(GatewayStore store)
    {
        var device = store.FindSensor(CriticalPowerDeviceId)
            ?? throw new InvalidOperationException($"Power priority target '{CriticalPowerDeviceId}' is not registered.");

        var ingest = store.GetIngestState(device.Id)
            ?? throw new InvalidOperationException($"No ingest state for '{device.Id}' — seed the normal stream first.");

        var parkedPacket = new TelemetryPacket<PowerReading>
        {
            DeviceId = device.Id,
            Timestamp = (ingest.LastIngestedAt ?? DateTimeOffset.UtcNow).AddSeconds(10),
            Sequence = ingest.LastSequence + 1,
            SignalQuality = 88,
            Payload = new PowerReading(1200, PowerMetric.Watts)
        };

        store.EnqueueStandardWithoutDrain(QueuedTelemetryItem.FromPower(parkedPacket));

        if (store.StandardIngestQueue.Depth < 1)
        {
            throw new InvalidOperationException("Expected a parked FIFO power packet before the priority bypass.");
        }

        var fifoDepthBefore = store.StandardIngestQueue.Depth;
        var priorityDequeuedBefore = store.PriorityAlertQueue.TotalDequeued;

        var accepted = store.RaisePriorityAlert(new PriorityAlert
        {
            DeviceId = device.Id,
            MacAddress = device.MacAddress,
            Severity = AlertSeverity.Critical,
            Reason = "Severe power spike on site inverter — priority bypass ahead of FIFO backlog",
            RaisedAt = DateTimeOffset.UtcNow
        });

        if (!accepted)
        {
            throw new InvalidOperationException("Critical power alert was suppressed unexpectedly.");
        }

        if (store.PriorityAlertQueue.TotalDequeued != priorityDequeuedBefore + 1)
        {
            throw new InvalidOperationException("Critical power alert was not dequeued on the priority path.");
        }

        if (store.StandardIngestQueue.Depth != fifoDepthBefore)
        {
            throw new InvalidOperationException("FIFO depth changed during power priority bypass.");
        }

        if (store.PriorityAlertQueue.LastBypassReason is null
            || !store.PriorityAlertQueue.LastBypassReason.Contains("power spike", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("LastPriorityBypassReason was not updated for the power spike.");
        }

        store.DrainPendingQueues();
    }
}
