using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Simulation;

/// <summary>
/// Bootstraps search → command sequences so Suggested Actions appear on a fresh API start
/// (brief example: moisture context followed by pump search / StartPump).
/// </summary>
public static class RecommendationSeeder
{
    public static void SeedOperatorPatterns(GatewayStore store)
    {
        // Repeat the moisture → pump pattern so the frequency map has a clear signal.
        for (var i = 0; i < 3; i++)
        {
            store.RecordOperatorSearch(new RecordSearchRequest
            {
                CategoryFilter = nameof(SensorCategory.Environmental),
                HealthFilter = nameof(HealthState.Critical),
                AlertScope = "unique",
                RelatedDeviceId = TelemetryFaultSeeder.CriticalMoistureDeviceId
            });

            store.RecordOperatorSearch(new RecordSearchRequest
            {
                CategoryFilter = nameof(SensorCategory.Actuator),
                HealthFilter = string.Empty,
                AlertScope = "any",
                RelatedDeviceId = "sx-act-sump-pump"
            });

            var issued = store.TryIssueCommand(new IssueCommandRequest
            {
                DeviceId = "sx-act-sump-pump",
                Kind = CommandKind.StartPump,
                OperatorNote = $"Seeded pattern {i + 1}: moisture drop → start sump pump"
            });

            if (!issued.Succeeded)
            {
                throw new InvalidOperationException(
                    "Recommendation seed could not issue StartPump: " + string.Join(" ", issued.Errors));
            }

            // Leave the stack tidy for the demo undo KPI (optional pop).
            store.TryUndoCommand();
        }

        var suggestions = store.GetSuggestions();
        if (suggestions.Suggestions.Count == 0)
        {
            throw new InvalidOperationException("Recommendation seed produced no suggestions.");
        }

        if (suggestions.SearchHistoryCount < 6)
        {
            throw new InvalidOperationException("Recommendation seed did not record enough search history.");
        }
    }
}
