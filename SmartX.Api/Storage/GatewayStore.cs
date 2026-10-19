using SmartX.Api.Simulation;
using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// In-memory gateway state for the simulation. A singleton so every request
/// sees the same registered ESP32 devices until the process restarts.
/// </summary>
public sealed class GatewayStore
{
    private readonly object _gate = new();

    public GatewayStore()
    {
        DeploymentRoots = DefaultDeploymentTree.Create();
        var tree = DeploymentTreeValidator.ValidateForest(DeploymentRoots);
        if (!tree.IsValid)
        {
            throw new InvalidOperationException(
                "Default deployment tree failed recursive validation: " + string.Join(" ", tree.Errors));
        }

        FleetSeeder.SeedDevices(this);
        TelemetryStreamSeeder.SeedNormalStreams(this);
        TelemetryFaultSeeder.SeedTemperatureSpike(this);
        TelemetryFaultSeeder.SeedStuckActuator(this);
        TelemetryFaultSeeder.SeedSilentSensor(this);
        TelemetryFaultSeeder.SeedMalformedPackets(this);
        TelemetryFaultSeeder.SeedLocationOutage(this);
        TelemetryFaultSeeder.SeedCriticalMoisturePriorityBypass(this);
        TelemetryFaultSeeder.SeedRecovery(this);
    }

    public List<DeploymentNode> DeploymentRoots { get; }

    /// <summary>Custom fleet collection: lookup + ingest counters, not a bare List.</summary>
    public SensorFleet Fleet { get; } = new();

    /// <summary>Standard FIFO ingest queue. Packets enqueue here, then drain into typed buffers.</summary>
    public StandardTelemetryQueue StandardIngestQueue { get; } = new();

    /// <summary>Critical alerts bypass the FIFO stream via this priority queue.</summary>
    public CriticalAlertQueue PriorityAlertQueue { get; } = new();

    /// <summary>Manual override undo stack. Issue/Undo APIs wire this in Stage 4.2–4.3.</summary>
    public CommandUndoStack CommandStack { get; } = new();

    /// <summary>Alerts already handled on the priority path (newest last).</summary>
    public List<PriorityAlert> ProcessedPriorityAlerts { get; } = [];

    // Separate typed buffers — never List<object> — so float/int/bool packets stay unboxed.
    public List<TelemetryPacket<EnvironmentalReading>> EnvironmentalPackets { get; } = [];

    public List<TelemetryPacket<PowerReading>> PowerPackets { get; } = [];

    public List<TelemetryPacket<ActuatorReading>> ActuatorPackets { get; } = [];

    // Flattened numeric lists copied from jagged/window arrays for collection-based queries.
    public List<float> PromotedEnvironmentalValues { get; } = [];

    public List<int> PromotedPowerValues { get; } = [];

    public List<bool> PromotedActuatorValues { get; } = [];

    // Raw sequential batches land here as arrays before they are queried as List<T>.
    public RawTelemetryBatches RawBatches { get; } = new();

    // Refused packets stay out of the typed buffers; operators still see that garbage arrived.
    public List<TelemetryRejection> Rejections { get; } = [];

    // File bytes stay on disk; this list is metadata so ingest buffers never hold photos or logs.
    private readonly List<SensorAttachment> _attachments = [];

    public IReadOnlyList<SensorDevice> SnapshotSensors()
    {
        lock (_gate)
        {
            RefreshFreshnessLocked();
            return Fleet.Snapshot();
        }
    }

    public Task<IReadOnlyList<SensorDevice>> SnapshotSensorsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SnapshotSensors());
    }

    /// <summary>
    /// Operator diagnostics for FIFO vs priority bypass. Undo / HashSet counts stay 0 until later stages.
    /// </summary>
    public StreamSnapshotResponse GetStreamSnapshot()
    {
        lock (_gate)
        {
            const int recentTake = 8;
            var recentAlerts = ProcessedPriorityAlerts.Count <= recentTake
                ? ProcessedPriorityAlerts
                : ProcessedPriorityAlerts.Skip(ProcessedPriorityAlerts.Count - recentTake);

            var recentCommands = CommandStack.SnapshotNewestFirst().Take(recentTake);

            return new StreamSnapshotResponse
            {
                StandardQueueDepth = StandardIngestQueue.Depth,
                PriorityQueueDepth = PriorityAlertQueue.Depth,
                UndoStackDepth = CommandStack.Depth,
                UniqueActiveAlertCount = 0,
                LastStandardProcessedAt = StandardIngestQueue.LastDequeuedAt,
                LastPriorityProcessedAt = PriorityAlertQueue.LastDequeuedAt,
                LastPriorityBypassReason = PriorityAlertQueue.LastBypassReason,
                RecentCommands = recentCommands.Select(StreamDtoMapper.ToCommandResponse).ToList(),
                RecentAlerts = recentAlerts.Select(StreamDtoMapper.ToAlertResponse).ToList()
            };
        }
    }

    public IReadOnlyList<PriorityAlert> SnapshotProcessedPriorityAlerts()
    {
        lock (_gate)
        {
            return [.. ProcessedPriorityAlerts];
        }
    }

    /// <summary>
    /// Issues a manual override: push onto the undo stack and apply ExpectedIsActive on the actuator.
    /// </summary>
    public IssueCommandResponse TryIssueCommand(IssueCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.DeviceId))
            {
                errors.Add("Device id is required.");
            }

            if (!Enum.IsDefined(request.Kind))
            {
                errors.Add("Command kind is not recognised.");
            }

            if (errors.Count > 0)
            {
                return FailIssue(errors);
            }

            // O(1) Dictionary resolve by id or MAC — no fleet scan.
            var device = Fleet.ResolveLive(request.DeviceId);
            if (device is null)
            {
                return FailIssue(["Device is not registered on this gateway."]);
            }

            if (device.Category != SensorCategory.Actuator)
            {
                return FailIssue(["Manual overrides only target Actuator-category devices (valves, pumps, relays)."]);
            }

            var previous = device.ExpectedIsActive;
            var target = StreamDtoMapper.ResolveTargetIsActive(request.Kind, previous);
            var command = new DeviceCommand
            {
                Id = Guid.NewGuid().ToString("N"),
                DeviceId = device.Id,
                MacAddress = device.MacAddress,
                Kind = request.Kind,
                TargetIsActive = target,
                PreviousIsActive = previous,
                IssuedAt = DateTimeOffset.UtcNow,
                OperatorNote = string.IsNullOrWhiteSpace(request.OperatorNote)
                    ? null
                    : request.OperatorNote.Trim()
            };

            CommandStack.Push(command);
            device.ExpectedIsActive = target;

            return new IssueCommandResponse
            {
                Succeeded = true,
                Command = StreamDtoMapper.ToCommandResponse(command)
            };
        }
    }

    public Task<IssueCommandResponse> TryIssueCommandAsync(
        IssueCommandRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TryIssueCommand(request));
    }

    /// <summary>
    /// Pops the last manual override and restores PreviousIsActive on the target actuator.
    /// Empty stack returns a clear error — never throws.
    /// </summary>
    public UndoCommandResponse TryUndoCommand()
    {
        lock (_gate)
        {
            if (!CommandStack.TryPop(out var command) || command is null)
            {
                return new UndoCommandResponse
                {
                    Succeeded = false,
                    Errors = ["Nothing to undo — the command stack is empty."]
                };
            }

            var device = Fleet.FindById(command.DeviceId);
            if (device is not null)
            {
                device.ExpectedIsActive = command.PreviousIsActive;
            }

            return new UndoCommandResponse
            {
                Succeeded = true,
                UndoneCommand = StreamDtoMapper.ToCommandResponse(command)
            };
        }
    }

    public Task<UndoCommandResponse> TryUndoCommandAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TryUndoCommand());
    }

    private static IssueCommandResponse FailIssue(List<string> errors)
        => new() { Succeeded = false, Errors = errors };

    public bool TryAdd(SensorDevice device, out string? error)
    {
        lock (_gate)
        {
            return Fleet.TryAdd(device, out error);
        }
    }

    public Task<StoreResult> TryAddAsync(SensorDevice device, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var succeeded = TryAdd(device, out var error);
        return Task.FromResult(new StoreResult(succeeded, error));
    }

    public Task<StoreResult> TryIngestEnvironmentalAsync(
        TelemetryPacket<EnvironmentalReading> packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var succeeded = TryIngestEnvironmental(packet, out var error);
        return Task.FromResult(new StoreResult(succeeded, error));
    }

    public Task<StoreResult> TryIngestPowerAsync(
        TelemetryPacket<PowerReading> packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var succeeded = TryIngestPower(packet, out var error);
        return Task.FromResult(new StoreResult(succeeded, error));
    }

    public Task<StoreResult> TryIngestActuatorAsync(
        TelemetryPacket<ActuatorReading> packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var succeeded = TryIngestActuator(packet, out var error);
        return Task.FromResult(new StoreResult(succeeded, error));
    }

    public bool TryIngestEnvironmental(TelemetryPacket<EnvironmentalReading> packet, out string? error)
        => TryEnqueueAndDrain(packet.DeviceId, QueuedTelemetryItem.FromEnvironmental(packet), out error);

    public bool TryIngestPower(TelemetryPacket<PowerReading> packet, out string? error)
        => TryEnqueueAndDrain(packet.DeviceId, QueuedTelemetryItem.FromPower(packet), out error);

    public bool TryIngestActuator(TelemetryPacket<ActuatorReading> packet, out string? error)
        => TryEnqueueAndDrain(packet.DeviceId, QueuedTelemetryItem.FromActuator(packet), out error);

    /// <summary>
    /// Standard path: validate device, enqueue on the FIFO queue, then drain so packets
    /// hit typed buffers in First-In-First-Out order (not List-append-only).
    /// </summary>
    private bool TryEnqueueAndDrain(string deviceId, QueuedTelemetryItem item, out string? error)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                error = "Packet device id is required.";
                return false;
            }

            if (Fleet.ResolveLive(deviceId) is null)
            {
                error = "Device is not registered on this gateway.";
                return false;
            }

            StandardIngestQueue.Enqueue(item);
            // Priority alerts always drain first so critical work bypasses FIFO backlog.
            DrainPriorityAlertsLocked();
            DrainStandardQueueLocked();
            error = null;
            return true;
        }
    }

    /// <summary>
    /// Simulation/demo: park a packet on the FIFO queue without draining it,
    /// so a priority alert can prove it bypasses backlog.
    /// </summary>
    public void EnqueueStandardWithoutDrain(QueuedTelemetryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_gate)
        {
            StandardIngestQueue.Enqueue(item);
        }
    }

    /// <summary>
    /// Drains priority alerts first, then the standard FIFO queue.
    /// </summary>
    public void DrainPendingQueues()
    {
        lock (_gate)
        {
            DrainPriorityAlertsLocked();
            DrainStandardQueueLocked();
        }
    }

    /// <summary>
    /// Raises an alert onto the priority queue and processes it immediately,
    /// without waiting behind standard FIFO telemetry.
    /// </summary>
    public void RaisePriorityAlert(PriorityAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);

        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(alert.Id))
            {
                alert.Id = Guid.NewGuid().ToString("N");
            }

            if (alert.RaisedAt == default)
            {
                alert.RaisedAt = DateTimeOffset.UtcNow;
            }

            if (string.IsNullOrWhiteSpace(alert.MacAddress)
                && !string.IsNullOrWhiteSpace(alert.DeviceId)
                && Fleet.FindById(alert.DeviceId) is { } device)
            {
                alert.MacAddress = device.MacAddress;
            }

            PriorityAlertQueue.Enqueue(alert);
            DrainPriorityAlertsLocked();
        }
    }

    public Task RaisePriorityAlertAsync(PriorityAlert alert, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RaisePriorityAlert(alert);
        return Task.CompletedTask;
    }

    private void DrainPriorityAlertsLocked()
    {
        while (PriorityAlertQueue.TryDequeue(out var alert) && alert is not null)
        {
            ApplyPriorityAlertLocked(alert);
        }
    }

    private void ApplyPriorityAlertLocked(PriorityAlert alert)
    {
        alert.IsProcessed = true;

        if (!string.IsNullOrWhiteSpace(alert.DeviceId)
            && Fleet.FindById(alert.DeviceId) is { } device
            && alert.Severity == AlertSeverity.Critical)
        {
            // Critical bypass marks the device so the dashboard can investigate immediately.
            device.Health = HealthState.Critical;
        }

        ProcessedPriorityAlerts.Add(alert);
    }

    private void DrainStandardQueueLocked()
    {
        while (StandardIngestQueue.TryDequeue(out var item) && item is not null)
        {
            ApplyQueuedItemLocked(item);
        }
    }

    private void ApplyQueuedItemLocked(QueuedTelemetryItem item)
    {
        switch (item.Category)
        {
            case SensorCategory.Environmental when item.Environmental is { } packet:
                ApplyAcceptedPacketLocked(
                    packet,
                    EnvironmentalPackets,
                    (payload, _) => TelemetryHealthClassifier.Classify(payload),
                    () => RawBatches.AppendEnvironmental(packet),
                    RawBatches.PromoteEnvironmentalPackets);
                break;

            case SensorCategory.PowerConsumption when item.Power is { } packet:
                ApplyAcceptedPacketLocked(
                    packet,
                    PowerPackets,
                    (payload, _) => TelemetryHealthClassifier.Classify(payload),
                    () => RawBatches.AppendPower(packet),
                    RawBatches.PromotePowerPackets);
                break;

            case SensorCategory.Actuator when item.Actuator is { } packet:
                ApplyAcceptedPacketLocked(
                    packet,
                    ActuatorPackets,
                    (payload, device) => TelemetryHealthClassifier.Classify(payload, device.ExpectedIsActive),
                    () => RawBatches.AppendActuator(packet),
                    RawBatches.PromoteActuatorPackets);
                break;
        }
    }

    private void ApplyAcceptedPacketLocked<T>(
        TelemetryPacket<T> packet,
        List<TelemetryPacket<T>> buffer,
        Func<T, SensorDevice, HealthState> classify,
        Action appendRawBatch,
        Func<List<TelemetryPacket<T>>> promoteToList)
        where T : struct
    {
        var device = Fleet.FindById(packet.DeviceId);
        if (device is null)
        {
            return;
        }

        // Arrays first, then copy the sequential batch into List<T> for queries.
        appendRawBatch();
        TelemetryCollectionPromoter.Replace(buffer, promoteToList());
        TelemetryCollectionPromoter.Replace(PromotedEnvironmentalValues, RawBatches.PromoteEnvironmentalValues());
        TelemetryCollectionPromoter.Replace(PromotedPowerValues, RawBatches.PromotePowerValues());
        TelemetryCollectionPromoter.Replace(PromotedActuatorValues, RawBatches.PromoteActuatorValues());
        device.LastSeenAt = packet.Timestamp == default ? DateTimeOffset.UtcNow : packet.Timestamp;
        device.Health = classify(packet.Payload, device);
        device.Freshness = TelemetryFreshnessClassifier.Classify(device.LastSeenAt, DateTimeOffset.UtcNow);
        Fleet.RecordIngest(device.Id, packet.Sequence, device.LastSeenAt.Value);
    }

    public SensorDevice? FindSensor(string id)
    {
        lock (_gate)
        {
            var device = Fleet.FindById(id);
            if (device is not null)
            {
                device.Freshness = TelemetryFreshnessClassifier.Classify(device.LastSeenAt, DateTimeOffset.UtcNow);
            }

            return device;
        }
    }

    public Task<SensorDevice?> FindSensorAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FindSensor(id));
    }

    public DeviceIngestState? GetIngestState(string id)
    {
        lock (_gate)
        {
            return Fleet.GetIngestState(id);
        }
    }

    public Task<DeviceIngestState?> GetIngestStateAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetIngestState(id));
    }

    /// <summary>
    /// Records a validator rejection. Last-seen and history do not change — a CRC failure is not a heartbeat.
    /// A known device is marked Invalid so the overview can show the exception.
    /// </summary>
    public void RecordRejection(string deviceId, IEnumerable<string> errors)
    {
        lock (_gate)
        {
            Rejections.Add(new TelemetryRejection
            {
                DeviceId = deviceId,
                RejectedAt = DateTimeOffset.UtcNow,
                Errors = [.. errors]
            });

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            var device = Fleet.FindById(deviceId);
            if (device is not null)
            {
                device.Health = HealthState.Invalid;
            }
        }
    }

    public Task RecordRejectionAsync(string deviceId, IEnumerable<string> errors, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RecordRejection(deviceId, errors);
        return Task.CompletedTask;
    }

    public IReadOnlyList<TelemetryRejection> SnapshotRejections(string? deviceId = null)
    {
        lock (_gate)
        {
            IEnumerable<TelemetryRejection> rows = Rejections;
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                rows = rows.Where(row =>
                    string.Equals(row.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            }

            return rows.ToList();
        }
    }

    public Task<IReadOnlyList<TelemetryRejection>> SnapshotRejectionsAsync(
        string? deviceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SnapshotRejections(deviceId));
    }

    public IReadOnlyList<SensorAttachment> SnapshotAttachments(string sensorId)
    {
        lock (_gate)
        {
            return _attachments
                .Where(row => string.Equals(row.SensorId, sensorId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(row => row.StoredAt)
                .ToList();
        }
    }

    public Task<IReadOnlyList<SensorAttachment>> SnapshotAttachmentsAsync(
        string sensorId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SnapshotAttachments(sensorId));
    }

    public SensorAttachment? FindAttachment(string sensorId, string attachmentId)
    {
        lock (_gate)
        {
            return _attachments.FirstOrDefault(row =>
                string.Equals(row.SensorId, sensorId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(row.Id, attachmentId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public Task<SensorAttachment?> FindAttachmentAsync(
        string sensorId,
        string attachmentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FindAttachment(sensorId, attachmentId));
    }

    public bool TryAddAttachment(SensorAttachment attachment, out string? error)
    {
        lock (_gate)
        {
            if (Fleet.FindById(attachment.SensorId) is null)
            {
                error = "Device is not registered on this gateway.";
                return false;
            }

            _attachments.Add(attachment);
            error = null;
            return true;
        }
    }

    public Task<StoreResult> TryAddAttachmentAsync(SensorAttachment attachment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var succeeded = TryAddAttachment(attachment, out var error);
        return Task.FromResult(new StoreResult(succeeded, error));
    }

    private void RefreshFreshnessLocked()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var sensor in Fleet)
        {
            sensor.Freshness = TelemetryFreshnessClassifier.Classify(sensor.LastSeenAt, now);
        }
    }

    public TelemetryHistoryResponse HistoryFor(SensorDevice device, int take)
    {
        lock (_gate)
        {
            var history = new TelemetryHistoryResponse
            {
                DeviceId = device.Id,
                Category = device.Category
            };

            switch (device.Category)
            {
                case SensorCategory.Environmental:
                    history.Environmental = Latest(EnvironmentalPackets, device.Id, take);
                    break;
                case SensorCategory.PowerConsumption:
                    history.Power = Latest(PowerPackets, device.Id, take);
                    break;
                case SensorCategory.Actuator:
                    history.Actuator = Latest(ActuatorPackets, device.Id, take);
                    break;
            }

            return history;
        }
    }

    public Task<TelemetryHistoryResponse> HistoryForAsync(
        SensorDevice device,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(HistoryFor(device, take));
    }

    public IReadOnlyList<TelemetryPacket<PowerReading>> SnapshotPowerPackets()
    {
        lock (_gate)
        {
            return PowerPackets.ToList();
        }
    }

    public Task<IReadOnlyList<TelemetryPacket<PowerReading>>> SnapshotPowerPacketsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SnapshotPowerPackets());
    }

    public IReadOnlyList<TelemetryPacket<EnvironmentalReading>> SnapshotEnvironmentalPackets(string deviceId)
    {
        lock (_gate)
        {
            return Latest(EnvironmentalPackets, deviceId, 50);
        }
    }

    public Task<IReadOnlyList<TelemetryPacket<EnvironmentalReading>>> SnapshotEnvironmentalPacketsAsync(
        string deviceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SnapshotEnvironmentalPackets(deviceId));
    }

    private static List<TelemetryPacket<T>> Latest<T>(
        List<TelemetryPacket<T>> buffer,
        string deviceId,
        int take)
        where T : struct
    {
        return buffer
            .Where(packet => string.Equals(packet.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(packet => packet.Timestamp)
            .Take(take)
            .ToList();
    }
}
