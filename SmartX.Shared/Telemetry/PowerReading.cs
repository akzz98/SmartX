using System.Text.Json.Serialization;

namespace SmartX.Shared;

/// <summary>
/// Integer telemetry from a PowerConsumption-category device.
/// Struct payload for TelemetryPacket&lt;PowerReading&gt;; later + overloading can aggregate two meters.
/// </summary>
public readonly struct PowerReading
{
    [JsonConstructor]
    public PowerReading(int value, PowerMetric metric)
    {
        Value = value;
        Metric = metric;
    }

    public int Value { get; }

    public PowerMetric Metric { get; }
}
