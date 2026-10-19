using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// Predictive action engine: records operator searches and command sequences, then
/// scores follow-up suggestions with a Dictionary frequency map of (from → to) pairs.
/// Example pattern: moisture-critical context often followed by searching/acting on the sump pump.
/// </summary>
public sealed class RecommendationEngine
{
    private readonly List<OperatorSearchEvent> _searches = [];
    private readonly List<string> _activityKeys = [];

    // Adjacency / frequency map: "fromKey|toKey" → count
    private readonly Dictionary<string, int> _sequenceCounts = new(StringComparer.OrdinalIgnoreCase);

    public int SearchHistoryCount => _searches.Count;

    public int SequencePairCount => _sequenceCounts.Count;

    public IReadOnlyList<OperatorSearchEvent> SnapshotSearches() => [.. _searches];

    public OperatorSearchEvent RecordSearch(RecordSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var patternKey = BuildSearchPatternKey(request);
        var evt = new OperatorSearchEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            SearchedAt = DateTimeOffset.UtcNow,
            CategoryFilter = Normalize(request.CategoryFilter),
            HealthFilter = Normalize(request.HealthFilter),
            AlertScope = Normalize(request.AlertScope),
            RelatedDeviceId = Normalize(request.RelatedDeviceId),
            PatternKey = patternKey
        };

        _searches.Add(evt);
        RecordActivity(patternKey);
        return evt;
    }

    public void RecordCommandIssued(DeviceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var key = $"command:{command.DeviceId}:{command.Kind}";
        RecordActivity(key);
    }

    public void RecordFaultContext(string deviceId, string faultKey)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(faultKey))
        {
            return;
        }

        RecordActivity($"{faultKey}:{deviceId}");
    }

    public SuggestionsResponse BuildSuggestions(
        IReadOnlyList<SensorDevice> fleet,
        IReadOnlyList<PriorityAlert> recentAlerts,
        IReadOnlyCollection<string> uniqueAlertKeys)
    {
        var suggestions = new List<SuggestionItem>();

        // 1) Pattern-based: strongest sequence pairs ending in a command or pump search.
        foreach (var (pairKey, count) in _sequenceCounts.OrderByDescending(kv => kv.Value).Take(8))
        {
            var parts = pairKey.Split('|', 2);
            if (parts.Length != 2 || count < 1)
            {
                continue;
            }

            var from = parts[0];
            var to = parts[1];

            if (to.StartsWith("command:", StringComparison.OrdinalIgnoreCase))
            {
                var commandParts = to.Split(':', 3);
                if (commandParts.Length == 3
                    && Enum.TryParse<CommandKind>(commandParts[2], ignoreCase: true, out var kind))
                {
                    suggestions.Add(new SuggestionItem
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Kind = "suggested-action",
                        Title = $"Pattern: after {Describe(from)}, operators issued {kind}",
                        Detail =
                            $"Seen {count} time(s). Consider {kind} on {commandParts[1]} " +
                            "based on search/command history (not a random tip).",
                        RelatedDeviceId = commandParts[1],
                        SuggestedCommand = kind,
                        Score = count * 10
                    });
                }
            }
            else if (to.Contains("pump", StringComparison.OrdinalIgnoreCase)
                     || to.Contains("actuator", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new SuggestionItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Kind = "automated-insight",
                    Title = $"Operators often search {Describe(to)} after {Describe(from)}",
                    Detail = $"Sequence frequency {count}. Open Actuator filters or the sump pump next.",
                    RelatedDeviceId = ExtractDeviceId(to),
                    Score = count * 8
                });
            }
        }

        // 2) Live fault highlights from unique HashSet / recent critical alerts.
        foreach (var key in uniqueAlertKeys.Take(5))
        {
            var deviceId = ExtractDeviceIdFromAlertKey(key);
            suggestions.Add(new SuggestionItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = "automated-insight",
                Title = "Active unique alert still open",
                Detail = $"HashSet key `{key}` is active. Investigate before searching the wider fleet.",
                RelatedDeviceId = deviceId,
                Score = 25
            });
        }

        foreach (var alert in recentAlerts.Where(a => a.Severity == AlertSeverity.Critical).TakeLast(3))
        {
            var pump = fleet.FirstOrDefault(d =>
                d.Category == SensorCategory.Actuator
                && d.Id.Contains("pump", StringComparison.OrdinalIgnoreCase));

            if (alert.DeviceId.Contains("moist", StringComparison.OrdinalIgnoreCase) && pump is not null)
            {
                suggestions.Add(new SuggestionItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Kind = "suggested-action",
                    Title = "Moisture fault → consider sump pump override",
                    Detail =
                        $"{alert.DeviceId}: {alert.Reason}. Historical pattern links moisture drops " +
                        $"to {pump.Id} ({nameof(CommandKind.StartPump)}).",
                    RelatedDeviceId = pump.Id,
                    SuggestedCommand = CommandKind.StartPump,
                    Score = 40
                });
            }
            else
            {
                suggestions.Add(new SuggestionItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Kind = "automated-insight",
                    Title = $"Critical: {alert.DeviceId}",
                    Detail = alert.Reason,
                    RelatedDeviceId = alert.DeviceId,
                    Score = 30
                });
            }
        }

        // 3) Problematic devices currently Critical/Invalid.
        foreach (var device in fleet.Where(d => d.Health is HealthState.Critical or HealthState.Invalid).Take(5))
        {
            suggestions.Add(new SuggestionItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = "automated-insight",
                Title = $"Device health {device.Health}: {device.Id}",
                Detail = $"Category {device.Category}. Highlighted before you search — gateway-classified state.",
                RelatedDeviceId = device.Id,
                Score = 20
            });
        }

        var ranked = suggestions
            .GroupBy(s => s.Title + "|" + s.RelatedDeviceId)
            .Select(g => g.OrderByDescending(s => s.Score).First())
            .OrderByDescending(s => s.Score)
            .Take(10)
            .ToList();

        return new SuggestionsResponse
        {
            Suggestions = ranked,
            SearchHistoryCount = _searches.Count,
            SequencePairCount = _sequenceCounts.Count
        };
    }

    private void RecordActivity(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (_activityKeys.Count > 0)
        {
            var from = _activityKeys[^1];
            var pair = $"{from}|{key}";
            _sequenceCounts[pair] = _sequenceCounts.TryGetValue(pair, out var n) ? n + 1 : 1;
        }

        _activityKeys.Add(key);
        if (_activityKeys.Count > 200)
        {
            _activityKeys.RemoveAt(0);
        }
    }

    public static string BuildSearchPatternKey(RecordSearchRequest request)
    {
        var category = Normalize(request.CategoryFilter) ?? "any-category";
        var health = Normalize(request.HealthFilter) ?? "any-health";
        var scope = Normalize(request.AlertScope) ?? "any";
        var device = Normalize(request.RelatedDeviceId);

        if (!string.IsNullOrWhiteSpace(device)
            && device.Contains("moist", StringComparison.OrdinalIgnoreCase))
        {
            return $"search:moisture:{device}";
        }

        if (!string.IsNullOrWhiteSpace(device)
            && device.Contains("pump", StringComparison.OrdinalIgnoreCase))
        {
            return $"search:pump:{device}";
        }

        if (string.Equals(health, "Critical", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scope, "unique", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scope, "attention", StringComparison.OrdinalIgnoreCase))
        {
            return $"search:alert:{category}:{health}:{scope}";
        }

        if (string.Equals(category, "Actuator", StringComparison.OrdinalIgnoreCase))
        {
            return "search:actuator";
        }

        return $"search:{category}:{health}:{scope}";
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Describe(string key)
        => key.Replace(':', ' ');

    private static string? ExtractDeviceId(string key)
    {
        var parts = key.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[^1] : null;
    }

    private static string? ExtractDeviceIdFromAlertKey(string key)
    {
        // error:deviceId:reason  or  disconnect:deviceId
        var parts = key.Split(':', 3);
        if (parts.Length >= 2
            && (parts[0].Equals("error", StringComparison.OrdinalIgnoreCase)
                || parts[0].Equals("disconnect", StringComparison.OrdinalIgnoreCase)))
        {
            return parts[1];
        }

        return null;
    }
}
