namespace SmartX.Shared;

/// <summary>
/// Which environmental float a hydroponic or climate sensor published.
/// Kept separate from the numeric value so TelemetryPacket&lt;EnvironmentalReading&gt; stays a struct.
/// </summary>
public enum EnvironmentalMetric
{
    Temperature = 0,
    Moisture = 1,
    Ph = 2
}
