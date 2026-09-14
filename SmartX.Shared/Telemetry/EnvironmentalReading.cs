using System.Text.Json.Serialization;

namespace SmartX.Shared;

/// <summary>
/// Float telemetry from an Environmental-category device (soil moisture, °C, pH).
/// This is a struct so it can be T in TelemetryPacket&lt;T&gt; without boxing.
/// </summary>
public readonly struct EnvironmentalReading
{
    [JsonConstructor]
    public EnvironmentalReading(float value, EnvironmentalMetric metric)
    {
        Value = value;
        Metric = metric;
    }

    public float Value { get; }

    public EnvironmentalMetric Metric { get; }

    /// <summary>
    /// Spike/excursion between two samples of the same metric (current − previous).
    /// </summary>
    public static EnvironmentalReading operator -(EnvironmentalReading current, EnvironmentalReading previous)
    {
        if (current.Metric != previous.Metric)
        {
            throw new InvalidOperationException("Cannot subtract environmental readings that use different metrics.");
        }

        return new EnvironmentalReading(current.Value - previous.Value, current.Metric);
    }
}
