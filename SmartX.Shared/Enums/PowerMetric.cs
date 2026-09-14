namespace SmartX.Shared;

/// <summary>
/// Integer power quantities from a PowerConsumption device (smart meter, inverter).
/// Integers avoid float noise on wattage counters that ESP32 firmware typically send as int.
/// </summary>
public enum PowerMetric
{
    Watts = 0,
    WattHours = 1
}
