namespace SmartX.Api.Storage;

/// <summary>
/// Separate background consumer for the standard FIFO and priority alert queues.
/// Request threads only enqueue; this worker dequeues and applies work
/// (Final PoE lecturer feedback: do not drain queues as they are filled).
/// </summary>
public sealed class TelemetryQueueConsumer : BackgroundService
{
    private static readonly TimeSpan IdlePoll = TimeSpan.FromMilliseconds(250);

    private readonly GatewayStore _store;
    private readonly ILogger<TelemetryQueueConsumer> _logger;

    public TelemetryQueueConsumer(GatewayStore store, ILogger<TelemetryQueueConsumer> logger)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Telemetry queue consumer started (priority alerts first, then FIFO; tie-break = earlier RaisedAt).");

        // Catch anything left after GatewayStore seeding before the first Notify.
        _store.ProcessQueuedWork();
        _store.DetectAndRaiseDisconnects();
        _store.ProcessQueuedWork();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _store.WorkSignal.WaitAsync(IdlePoll, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var detected = _store.DetectAndRaiseDisconnects();
            var processed = _store.ProcessQueuedWork();
            if (detected > 0 || processed > 0)
            {
                _logger.LogDebug(
                    "Queue consumer: {Detected} new disconnect(s), applied {Processed} queued item(s).",
                    detected,
                    processed);
            }
        }
    }
}
