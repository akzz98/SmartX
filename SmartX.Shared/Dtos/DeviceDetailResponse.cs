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

    /// <summary>Malformed packets for this device that the gateway refused to store.</summary>
    public List<TelemetryRejection> Rejections { get; set; } = [];

    /// <summary>Commanded actuator position, when this device is a valve or switch.</summary>
    public bool? ExpectedIsActive { get; set; }

    /// <summary>Rack photos, configs and hardware logs stored against this ESP32.</summary>
    public List<AttachmentResponse> Attachments { get; set; } = [];
}
