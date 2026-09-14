using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// First landing zone for sequential telemetry. Jagged rows hold a variable-length
/// history per ESP32; the rectangular window holds a fixed batch of the last samples
/// across temperature, moisture, and pH. Promote*() copies those arrays into List&lt;T&gt;.
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

    private TelemetryPacket<EnvironmentalReading>[] _environmentalPackets = [];
    private TelemetryPacket<PowerReading>[] _powerPackets = [];
    private TelemetryPacket<ActuatorReading>[] _actuatorPackets = [];

    public float[][] EnvironmentalSamples => _environmentalSamples;
    public int[][] PowerSamples => _powerSamples;
    public bool[][] ActuatorSamples => _actuatorSamples;
    public float[,] EnvironmentalWindow => _environmentalWindow;
    public int EnvironmentalWindowFilled => _windowFilled;

    public void AppendEnvironmental(TelemetryPacket<EnvironmentalReading> packet)
    {
        AppendJagged(ref _environmentalDeviceIds, ref _environmentalSamples, packet.DeviceId, packet.Payload.Value);
        _environmentalWindow[_windowWriteIndex, (int)packet.Payload.Metric] = packet.Payload.Value;
        _windowWriteIndex = (_windowWriteIndex + 1) % EnvironmentalWindowLength;
        if (_windowFilled < EnvironmentalWindowLength)
        {
            _windowFilled++;
        }

        AppendPacket(ref _environmentalPackets, packet);
    }

    public void AppendPower(TelemetryPacket<PowerReading> packet)
    {
        AppendJagged(ref _powerDeviceIds, ref _powerSamples, packet.DeviceId, packet.Payload.Value);
        AppendPacket(ref _powerPackets, packet);
    }

    public void AppendActuator(TelemetryPacket<ActuatorReading> packet)
    {
        AppendJagged(ref _actuatorDeviceIds, ref _actuatorSamples, packet.DeviceId, packet.Payload.IsActive);
        AppendPacket(ref _actuatorPackets, packet);
    }

    public List<TelemetryPacket<EnvironmentalReading>> PromoteEnvironmentalPackets()
        => TelemetryCollectionPromoter.FromArray(_environmentalPackets);

    public List<TelemetryPacket<PowerReading>> PromotePowerPackets()
        => TelemetryCollectionPromoter.FromArray(_powerPackets);

    public List<TelemetryPacket<ActuatorReading>> PromoteActuatorPackets()
        => TelemetryCollectionPromoter.FromArray(_actuatorPackets);

    public List<float> PromoteEnvironmentalValues()
        => TelemetryCollectionPromoter.FromJagged(_environmentalSamples);

    public List<int> PromotePowerValues()
        => TelemetryCollectionPromoter.FromJagged(_powerSamples);

    public List<bool> PromoteActuatorValues()
        => TelemetryCollectionPromoter.FromJagged(_actuatorSamples);

    public List<float> PromoteEnvironmentalWindow()
        => TelemetryCollectionPromoter.FromWindow(_environmentalWindow, _windowFilled);

    private static void AppendPacket<T>(ref T[] batch, T packet)
    {
        Array.Resize(ref batch, batch.Length + 1);
        batch[^1] = packet;
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
