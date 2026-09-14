namespace SmartX.Shared;

/// <summary>
/// Float telemetry from an Environmental-category device (soil moisture, °C, pH).
/// This is a struct so it can be T in TelemetryPacket&lt;T&gt; without boxing.
/// </summary>
public readonly struct EnvironmentalReading
{
    public EnvironmentalReading(float value, EnvironmentalMetric metric)
    {
        Value = value;
        Metric = metric;
    }

    public float Value { get; }

    public EnvironmentalMetric Metric { get; }
}
