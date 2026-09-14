namespace SmartX.Shared;

/// <summary>
/// Result of ingesting a packet. Health and freshness are filled by the gateway
/// after validation; a failed ingest returns Errors instead.
/// </summary>
public sealed class IngestTelemetryResponse
{
    public bool Succeeded { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public HealthState? Health { get; set; }

    public FreshnessState? Freshness { get; set; }

    public List<string> Errors { get; set; } = [];
}
