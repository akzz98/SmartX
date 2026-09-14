using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// First landing zone for sequential telemetry. Jagged rows hold a variable-length
/// history per ESP32; the rectangular window holds a fixed batch of the last samples
/// across temperature, moisture, and pH. List&lt;T&gt; promotion is the next checklist step.
/// </summary>
public sealed class RawTelemetryBatches
{
    public const int EnvironmentalWindowLength = 16;
    public const int EnvironmentalMetricCount = 3;

    private string[] _environmentalDeviceIds = [];
    private float[][] _environmentalSamples = [];

    private string[] _powerDeviceIds = [];
    private int[][] _powerSamples = [];

    private string[] _actuatorDeviceIds = [];
    private bool[][] _actuatorSamples = [];

    // [sampleIndex, metricIndex] — metric 0 = temperature, 1 = moisture, 2 = pH
    private readonly float[,] _environmentalWindow = new float[EnvironmentalWindowLength, EnvironmentalMetricCount];
    private int _windowWriteIndex;
    private int _windowFilled;

    public float[][] EnvironmentalSamples => _environmentalSamples;
    public int[][] PowerSamples => _powerSamples;
    public bool[][] ActuatorSamples => _actuatorSamples;
    public float[,] EnvironmentalWindow => _environmentalWindow;
    public int EnvironmentalWindowFilled => _windowFilled;

    public void AppendEnvironmental(string deviceId, EnvironmentalReading reading)
    {
        AppendJagged(ref _environmentalDeviceIds, ref _environmentalSamples, deviceId, reading.Value);
        _environmentalWindow[_windowWriteIndex, (int)reading.Metric] = reading.Value;
        _windowWriteIndex = (_windowWriteIndex + 1) % EnvironmentalWindowLength;
        if (_windowFilled < EnvironmentalWindowLength)
        {
            _windowFilled++;
        }
    }

    public void AppendPower(string deviceId, PowerReading reading)
    {
        AppendJagged(ref _powerDeviceIds, ref _powerSamples, deviceId, reading.Value);
    }

    public void AppendActuator(string deviceId, ActuatorReading reading)
    {
        AppendJagged(ref _actuatorDeviceIds, ref _actuatorSamples, deviceId, reading.IsActive);
    }

    private static void AppendJagged<T>(
        ref string[] deviceIds,
        ref T[][] samples,
        string deviceId,
        T value)
    {
        var row = Array.FindIndex(deviceIds, id => string.Equals(id, deviceId, StringComparison.OrdinalIgnoreCase));
        if (row < 0)
        {
            row = deviceIds.Length;
            Array.Resize(ref deviceIds, row + 1);
            Array.Resize(ref samples, row + 1);
            deviceIds[row] = deviceId;
            samples[row] = [];
        }

        var history = samples[row];
        Array.Resize(ref history, history.Length + 1);
        history[^1] = value;
        samples[row] = history;
    }
}
