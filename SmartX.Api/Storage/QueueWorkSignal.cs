namespace SmartX.Api.Storage;

/// <summary>
/// Wakes <see cref="TelemetryQueueConsumer"/> when ingest or priority alerts enqueue work.
/// Caps at one pending signal so a burst does not flood the semaphore.
/// </summary>
public sealed class QueueWorkSignal
{
    private readonly SemaphoreSlim _ready = new(0, 1);

    public void Notify()
    {
        if (_ready.CurrentCount != 0)
        {
            return;
        }

        try
        {
            _ready.Release();
        }
        catch (SemaphoreFullException)
        {
            // Another notifier won the race — already awake.
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        => _ready.WaitAsync(timeout, cancellationToken);
}
