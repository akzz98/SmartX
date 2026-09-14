namespace SmartX.Shared;

/// <summary>
/// Rejects malformed, mistyped, or out-of-range telemetry before the gateway stores a packet.
/// Classification of health/freshness is a separate step after a packet is accepted.
/// </summary>
public static class TelemetryPacketValidator
{
    public static ValidationResult Validate(
        TelemetryPacket<EnvironmentalReading> packet,
        SensorDevice? device)
    {
        var result = ValidateEnvelope(packet, device, SensorCategory.Environmental);
        if (!Enum.IsDefined(packet.Payload.Metric))
        {
            result.AddError("Environmental metric is not temperature, moisture, or pH.");
            return result;
        }

        var value = packet.Payload.Value;
        switch (packet.Payload.Metric)
        {
            case EnvironmentalMetric.Temperature when value is < -20f or > 80f:
                result.AddError("Temperature must be between -20 and 80 °C for this hydroponic fleet.");
                break;
            case EnvironmentalMetric.Moisture when value is < 0f or > 100f:
                result.AddError("Soil moisture must be a percentage between 0 and 100.");
                break;
            case EnvironmentalMetric.Ph when value is < 0f or > 14f:
                result.AddError("pH must be between 0 and 14.");
                break;
        }

        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            result.AddError("Environmental reading is not a finite number.");
        }

        return result;
    }

    public static ValidationResult Validate(
        TelemetryPacket<PowerReading> packet,
        SensorDevice? device)
    {
        var result = ValidateEnvelope(packet, device, SensorCategory.PowerConsumption);
        if (!Enum.IsDefined(packet.Payload.Metric))
        {
            result.AddError("Power metric is not watts or watt-hours.");
            return result;
        }

        var value = packet.Payload.Value;
        if (value < 0)
        {
            result.AddError("Power readings cannot be negative.");
        }

        if (packet.Payload.Metric == PowerMetric.Watts && value > 100_000)
        {
            result.AddError("Wattage is outside the 0–100000 W range expected from a site meter.");
        }

        return result;
    }

    public static ValidationResult Validate(
        TelemetryPacket<ActuatorReading> packet,
        SensorDevice? device)
    {
        var result = ValidateEnvelope(packet, device, SensorCategory.Actuator);
        if (!Enum.IsDefined(packet.Payload.Kind))
        {
            result.AddError("Actuator kind is not valve or switch.");
        }

        return result;
    }

    private static ValidationResult ValidateEnvelope<T>(
        TelemetryPacket<T> packet,
        SensorDevice? device,
        SensorCategory expectedCategory)
        where T : struct
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(packet.DeviceId))
        {
            result.AddError("Packet device id is required.");
        }

        if (packet.Timestamp == default)
        {
            result.AddError("Packet timestamp is required.");
        }

        if (packet.Sequence < 0)
        {
            result.AddError("Packet sequence cannot be negative.");
        }

        if (packet.SignalQuality is > 100)
        {
            result.AddError("Signal quality must be 0–100 when provided.");
        }

        if (device is null)
        {
            result.AddError("Device is not registered on this gateway.");
            return result;
        }

        if (device.Category != expectedCategory)
        {
            result.AddError($"This device is registered as {device.Category}, so it cannot ingest {expectedCategory} packets.");
        }

        return result;
    }
}
