using SmartX.Shared;

namespace SmartX.Client;

/// <summary>
/// Operator-facing copy of gateway bands. The UI does not classify packets; it only explains them.
/// </summary>
public static class GatewayBands
{
    public static string Describe(TelemetryHistoryResponse? history, bool? expectedIsActive)
    {
        if (history?.Environmental.Count > 0)
        {
            return history.Environmental[0].Payload.Metric switch
            {
                EnvironmentalMetric.Temperature => "Temperature: Normal 18–26 °C; warning 10–18 / 26–40; critical outside that (valid packet range −20–80).",
                EnvironmentalMetric.Moisture => "Moisture: Normal 40–70%; warning 25–40 / 70–85; critical outside that.",
                EnvironmentalMetric.Ph => "pH: Normal 5.5–6.5; warning 5.0–5.5 / 6.5–7.2; critical outside that (valid 0–14).",
                _ => "Unknown environmental metric."
            };
        }

        if (history?.Power.Count > 0)
        {
            return history.Power[0].Payload.Metric == PowerMetric.Watts
                ? "Watts: warning above 3000 W; critical above 8000 W."
                : "Watt-hours are informational (not an exception band).";
        }

        if (expectedIsActive is { } expected)
        {
            return expected
                ? "Actuator is commanded active/open. A closed reading is classified Critical (stuck)."
                : "Actuator is commanded inactive/closed. An open reading is classified Critical (stuck).";
        }

        return "No actuator command state is configured for this device.";
    }
}
