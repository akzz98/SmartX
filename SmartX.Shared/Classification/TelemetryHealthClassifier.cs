namespace SmartX.Shared;

/// <summary>
/// Maps a validated reading onto HealthState using hydroponic / site-meter bands.
/// The dashboard only displays this; it does not compute it.
/// Values outside these bands but still accepted by TelemetryPacketValidator are Critical.
/// </summary>
public static class TelemetryHealthClassifier
{
    public static HealthState Classify(EnvironmentalReading reading)
    {
        return reading.Metric switch
        {
            // Typical NFT/DWC crop band; excursion is the spike operators drill into.
            EnvironmentalMetric.Temperature => FromBands(reading.Value, criticalLow: 10f, warnLow: 18f, warnHigh: 26f, criticalHigh: 40f),
            EnvironmentalMetric.Moisture => FromBands(reading.Value, criticalLow: 25f, warnLow: 40f, warnHigh: 70f, criticalHigh: 85f),
            EnvironmentalMetric.Ph => FromBands(reading.Value, criticalLow: 5.0f, warnLow: 5.5f, warnHigh: 6.5f, criticalHigh: 7.2f),
            _ => HealthState.Invalid
        };
    }

    public static HealthState Classify(PowerReading reading)
    {
        return reading.Metric switch
        {
            PowerMetric.Watts => FromBands(reading.Value, criticalLow: 0f, warnLow: 0f, warnHigh: 3000f, criticalHigh: 8000f),
            // Cumulative energy is not an exception by itself.
            PowerMetric.WattHours => HealthState.Normal,
            _ => HealthState.Invalid
        };
    }

    /// <summary>
    /// A valve/switch that disagrees with the commanded state is treated as stuck (Critical).
    /// </summary>
    public static HealthState Classify(ActuatorReading reading, bool? expectedIsActive)
    {
        if (expectedIsActive is { } expected && reading.IsActive != expected)
        {
            return HealthState.Critical;
        }

        return HealthState.Normal;
    }

    private static HealthState FromBands(float value, float criticalLow, float warnLow, float warnHigh, float criticalHigh)
    {
        if (value < criticalLow || value > criticalHigh)
        {
            return HealthState.Critical;
        }

        if (value < warnLow || value > warnHigh)
        {
            return HealthState.Warning;
        }

        return HealthState.Normal;
    }
}
