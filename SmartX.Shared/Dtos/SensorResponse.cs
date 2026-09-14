namespace SmartX.Shared;

/// <summary>
/// One sensor as returned to the dashboard (get or list row).
/// Health and freshness are gateway-classified so Progressive Disclosure can rank exceptions.
/// </summary>
public sealed class SensorResponse
{
    public string Id { get; set; } = string.Empty;

    public string MacAddress { get; set; } = string.Empty;

    public string LocationNodeId { get; set; } = string.Empty;

    public SensorCategory Category { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public HealthState Health { get; set; }

    public FreshnessState Freshness { get; set; }
}
