namespace SmartX.Shared;

/// <summary>
/// Classifies connectivity from last-seen time so a silent ESP32 stays visible
/// as Aging/Stale/Disconnected instead of vanishing from the overview.
/// </summary>
public static class TelemetryFreshnessClassifier
{
    public static readonly TimeSpan LiveWindow = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan AgingWindow = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan StaleWindow = TimeSpan.FromMinutes(15);

    public static FreshnessState Classify(DateTimeOffset? lastSeenAt, DateTimeOffset utcNow)
    {
        if (lastSeenAt is null)
        {
            return FreshnessState.Disconnected;
        }

        var age = utcNow - lastSeenAt.Value;
        if (age < TimeSpan.Zero)
        {
            return FreshnessState.Live;
        }

        if (age <= LiveWindow)
        {
            return FreshnessState.Live;
        }

        if (age <= AgingWindow)
        {
            return FreshnessState.Aging;
        }

        if (age <= StaleWindow)
        {
            return FreshnessState.Stale;
        }

        return FreshnessState.Disconnected;
    }
}
