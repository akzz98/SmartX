namespace SmartX.Shared;

/// <summary>
/// Progressive Disclosure level 2: one device plus where it sits in the deployment tree.
/// </summary>
public sealed class DeviceDetailResponse
{
    public SensorResponse Sensor { get; set; } = new();

    public string? LocationName { get; set; }

    public DeploymentLevel? LocationLevel { get; set; }

    /// <summary>Current − previous environmental sample (operator -), for spike investigation.</summary>
    public EnvironmentalReading? LatestEnvironmentalDelta { get; set; }

    /// <summary>Packet volume and last sequence from the SensorFleet collection.</summary>
    public DeviceIngestState? Ingest { get; set; }
}
