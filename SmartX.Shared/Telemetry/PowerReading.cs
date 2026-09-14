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

    /// <summary>
    /// Combines two meters of the same quantity (e.g. rack A watts + rack B watts = site load).
    /// </summary>
    public static PowerReading operator +(PowerReading left, PowerReading right)
    {
        EnsureSameMetric(left, right);
        return new PowerReading(checked(left.Value + right.Value), left.Metric);
    }

    /// <summary>
    /// Load change between two samples of the same metric (now − previous).
    /// </summary>
    public static PowerReading operator -(PowerReading left, PowerReading right)
    {
        EnsureSameMetric(left, right);
        return new PowerReading(left.Value - right.Value, left.Metric);
    }

    public static bool operator >(PowerReading left, PowerReading right)
    {
        EnsureSameMetric(left, right);
        return left.Value > right.Value;
    }

    public static bool operator <(PowerReading left, PowerReading right)
    {
        EnsureSameMetric(left, right);
        return left.Value < right.Value;
    }

    private static void EnsureSameMetric(PowerReading left, PowerReading right)
    {
        if (left.Metric != right.Metric)
        {
            throw new InvalidOperationException("Cannot combine power readings that use different metrics (watts vs watt-hours).");
        }
    }
}
