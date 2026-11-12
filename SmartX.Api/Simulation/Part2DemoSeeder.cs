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

        // Leave the burst parked briefly as backlog evidence (PeakDepth already captured).
        // Seed/bootstrap helpers and TelemetryQueueConsumer drain afterward — request threads do not.
        if (store.StandardIngestQueue.Depth < FifoBurstPacketCount)
        {
            throw new InvalidOperationException("FIFO burst backlog should still be sitting on the queue before the consumer runs.");
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

        if (store.ProcessPriorityQueue() < 1
            || store.PriorityAlertQueue.TotalDequeued != priorityDequeuedBefore + 1)
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

    /// <summary>
    /// Raises the same disconnect alert twice for the silent sump sensor. The HashSet
    /// accepts the first and suppresses the duplicate so the UI is not spammed.
    /// </summary>
    public static void SeedDisconnectDuplicateSuppression(GatewayStore store)
    {
        var deviceId = TelemetryFaultSeeder.SilentSensorDeviceId;
        var device = store.FindSensor(deviceId)
            ?? throw new InvalidOperationException($"Disconnect-dedupe target '{deviceId}' is not registered.");

        var alert = new PriorityAlert
        {
            DeviceId = device.Id,
            MacAddress = device.MacAddress,
            Severity = AlertSeverity.Warning,
            Reason = "Device disconnected — no recent telemetry from sump probe",
            RaisedAt = DateTimeOffset.UtcNow
        };

        var processedBefore = store.ProcessedPriorityAlerts.Count;
        var uniqueBefore = store.ActiveUniqueAlerts.Count;

        var first = store.RaisePriorityAlert(alert);
        if (!first)
        {
            throw new InvalidOperationException("First disconnect alert should have been accepted by the HashSet.");
        }

        if (store.ProcessPriorityQueue() < 1)
        {
            throw new InvalidOperationException("First disconnect alert was not processed by the priority consumer path.");
        }

        if (store.ProcessedPriorityAlerts.Count != processedBefore + 1
            || store.ActiveUniqueAlerts.Count != uniqueBefore + 1)
        {
            throw new InvalidOperationException("First disconnect alert did not update processed list / unique set.");
        }

        if (!store.ActiveUniqueAlerts.Contains(UniqueAlertSet.DisconnectKey(device.Id)))
        {
            throw new InvalidOperationException("Expected disconnect: HashSet key after the first alert.");
        }

        var duplicate = store.RaisePriorityAlert(new PriorityAlert
        {
            DeviceId = device.Id,
            MacAddress = device.MacAddress,
            Severity = AlertSeverity.Warning,
            Reason = "Device disconnected — no recent telemetry from sump probe",
            RaisedAt = DateTimeOffset.UtcNow
        });

        if (duplicate)
        {
            throw new InvalidOperationException("Duplicate disconnect alert should have been suppressed by HashSet.");
        }

        if (store.ProcessedPriorityAlerts.Count != processedBefore + 1
            || store.ActiveUniqueAlerts.Count != uniqueBefore + 1)
        {
            throw new InvalidOperationException("Duplicate disconnect changed processed list or unique set — suppression failed.");
        }
    }

    /// <summary>Demo actuators on the command stream (valve / pump / relay).</summary>
    public static readonly string[] DemoActuatorIds =
    [
        "sx-act-rack1-valve",
        "sx-act-rack2-valve",
        "sx-act-sump-pump",
        "sx-act-feeder-switch"
    ];

    public const string DemoOverrideValveId = "sx-act-rack2-valve";

    /// <summary>
    /// Confirms seeded actuators exist, then runs one issue+undo on the demo valve so
    /// override/undo paths are proven before the operator opens the command stream.
    /// </summary>
    public static void SeedActuatorOverrideDemo(GatewayStore store)
    {
        foreach (var id in DemoActuatorIds)
        {
            var device = store.FindSensor(id)
                ?? throw new InvalidOperationException($"Demo actuator '{id}' is not registered.");

            if (device.Category != SensorCategory.Actuator)
            {
                throw new InvalidOperationException($"'{id}' must be Actuator category for override demos.");
            }
        }

        var depthBefore = store.CommandStack.Depth;
        var issued = store.TryIssueCommand(new IssueCommandRequest
        {
            DeviceId = DemoOverrideValveId,
            Kind = CommandKind.CloseValve,
            OperatorNote = "Part 2 demo: prove override + undo on rack 2 valve"
        });

        if (!issued.Succeeded || issued.Command is null)
        {
            throw new InvalidOperationException(
                "Demo override failed: " + string.Join(" ", issued.Errors));
        }

        if (store.CommandStack.Depth != depthBefore + 1)
        {
            throw new InvalidOperationException("Demo override did not push onto the undo stack.");
        }

        var undone = store.TryUndoCommand();
        if (!undone.Succeeded || undone.UndoneCommand?.DeviceId != DemoOverrideValveId)
        {
            throw new InvalidOperationException(
                "Demo undo failed: " + string.Join(" ", undone.Errors));
        }

        if (store.CommandStack.Depth != depthBefore)
        {
            throw new InvalidOperationException("Demo undo did not restore the previous stack depth.");
        }
    }

    /// <summary>
    /// Bootstraps moisture → pump search/command patterns so Suggested Actions appear
    /// immediately on a fresh API boot (no 20-minute manual warmup). README documents
    /// GET /api/stream/suggestions and the /commands insights panel.
    /// </summary>
    public static void SeedRecommendationPatterns(GatewayStore store)
    {
        RecommendationSeeder.SeedOperatorPatterns(store);

        var suggestions = store.GetSuggestions();
        if (suggestions.SearchHistoryCount < 6 || suggestions.Suggestions.Count == 0)
        {
            throw new InvalidOperationException(
                "Recommendation bootstrap did not produce searchable history and suggestions for the demo.");
        }

        var hasPumpHint = suggestions.Suggestions.Any(s =>
            (s.RelatedDeviceId?.Contains("pump", StringComparison.OrdinalIgnoreCase) ?? false)
            || (s.SuggestedCommand == CommandKind.StartPump)
            || s.Title.Contains("moisture", StringComparison.OrdinalIgnoreCase)
            || s.Title.Contains("pump", StringComparison.OrdinalIgnoreCase));

        if (!hasPumpHint)
        {
            throw new InvalidOperationException(
                "Expected a moisture/pump related suggestion after recommendation bootstrap.");
        }
    }

    /// <summary>
    /// Two equal-severity Warning alerts: newer is enqueued first, older RaisedAt second.
    /// Tie-breaker must dequeue the earlier RaisedAt first.
    /// </summary>
    public static void SeedPriorityTieBreaker(GatewayStore store)
    {
        var earlierDevice = store.FindSensor("sx-env-rack2-moist")
            ?? throw new InvalidOperationException("Tie-breaker earlier device missing.");
        var laterDevice = store.FindSensor("sx-env-rack3-temp")
            ?? throw new InvalidOperationException("Tie-breaker later device missing.");

        var now = DateTimeOffset.UtcNow;
        var later = new PriorityAlert
        {
            DeviceId = laterDevice.Id,
            MacAddress = laterDevice.MacAddress,
            Severity = AlertSeverity.Warning,
            Reason = "Tie-break later warning — rack 3 temp drift",
            RaisedAt = now
        };
        var earlier = new PriorityAlert
        {
            DeviceId = earlierDevice.Id,
            MacAddress = earlierDevice.MacAddress,
            Severity = AlertSeverity.Warning,
            Reason = "Tie-break earlier warning — rack 2 moisture drift",
            RaisedAt = now.AddSeconds(-30)
        };

        if (!store.RaisePriorityAlert(later) || !store.RaisePriorityAlert(earlier))
        {
            throw new InvalidOperationException("Tie-breaker seed alerts were suppressed unexpectedly.");
        }

        if (store.PriorityAlertQueue.Depth != 2)
        {
            throw new InvalidOperationException(
                $"Expected 2 waiting priority alerts before tie-break dequeue; was {store.PriorityAlertQueue.Depth}.");
        }

        if (!store.TryProcessNextPriorityAlert(out var first) || first is null)
        {
            throw new InvalidOperationException("Tie-breaker could not dequeue the first alert.");
        }

        if (!string.Equals(first.DeviceId, earlierDevice.Id, StringComparison.OrdinalIgnoreCase)
            || !first.Reason.Contains("earlier", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Tie-breaker failed: expected earlier alert first, got '{first.DeviceId}: {first.Reason}'.");
        }

        if (!store.TryProcessNextPriorityAlert(out var second) || second is null)
        {
            throw new InvalidOperationException("Tie-breaker could not dequeue the second alert.");
        }

        if (!string.Equals(second.DeviceId, laterDevice.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Tie-breaker failed: expected later alert second, got '{second.DeviceId}'.");
        }
    }

    public const string ConsumerBacklogDeviceId = "sx-env-rack2-temp";

    public const int ConsumerBacklogPacketCount = 4;

    /// <summary>
    /// Parks a small FIFO backlog with no drain so <see cref="TelemetryQueueConsumer"/>
    /// has real work after the host starts (lecturer: separate consumer).
    /// </summary>
    public static void SeedConsumerBacklog(GatewayStore store)
    {
        var device = store.FindSensor(ConsumerBacklogDeviceId)
            ?? throw new InvalidOperationException($"Consumer backlog target '{ConsumerBacklogDeviceId}' missing.");

        var ingest = store.GetIngestState(device.Id)
            ?? throw new InvalidOperationException($"No ingest state for '{device.Id}'.");

        var timestamp = (ingest.LastIngestedAt ?? DateTimeOffset.UtcNow).AddSeconds(2);
        var sequence = ingest.LastSequence;

        for (var i = 0; i < ConsumerBacklogPacketCount; i++)
        {
            sequence++;
            timestamp = timestamp.AddSeconds(1);
            var packet = new TelemetryPacket<EnvironmentalReading>
            {
                DeviceId = device.Id,
                Timestamp = timestamp,
                Sequence = sequence,
                SignalQuality = 90,
                Payload = new EnvironmentalReading(21.2f + (i * 0.02f), EnvironmentalMetric.Temperature)
            };

            var validation = TelemetryPacketValidator.Validate(packet, device);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Consumer backlog packet rejected: {string.Join(" ", validation.Errors)}");
            }

            store.EnqueueStandardWithoutDrain(QueuedTelemetryItem.FromEnvironmental(packet));
        }

        if (store.StandardIngestQueue.Depth < ConsumerBacklogPacketCount)
        {
            throw new InvalidOperationException(
                $"Expected consumer backlog depth >= {ConsumerBacklogPacketCount}; was {store.StandardIngestQueue.Depth}.");
        }
    }
}
