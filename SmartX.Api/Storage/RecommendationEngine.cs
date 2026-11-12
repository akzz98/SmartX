using SmartX.Shared;

namespace SmartX.Api.Storage;

/// <summary>
/// Predictive action engine: records operator searches and command sequences, then
/// scores follow-up suggestions with a Dictionary frequency map of (from → to) pairs.
/// Confidence is normalised to [0, 1] from relative pattern strength — not raw counts,
/// and not hard-coded device rules (e.g. moisture → pump).
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
        var maxPairCount = _sequenceCounts.Count == 0
            ? 1
            : Math.Max(1, _sequenceCounts.Values.Max());

        // 1) Pattern-based: strongest sequence pairs ending in a command or device search.
        foreach (var (pairKey, count) in _sequenceCounts.OrderByDescending(kv => kv.Value).Take(8))
        {
            var parts = pairKey.Split('|', 2);
            if (parts.Length != 2 || count < 1)
            {
                continue;
            }

            var from = parts[0];
            var to = parts[1];
            var confidence = NormalizeConfidence(count, maxPairCount);

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
                            $"Pattern support {count}/{maxPairCount} (confidence {confidence:0.00}). " +
                            $"Consider {kind} on {commandParts[1]} from learned search/command history.",
                        RelatedDeviceId = commandParts[1],
                        SuggestedCommand = kind,
                        Confidence = confidence
                    });
                }
            }
            else if (to.StartsWith("search:device:", StringComparison.OrdinalIgnoreCase)
                     || to.StartsWith("search:actuator", StringComparison.OrdinalIgnoreCase)
                     || to.Contains("actuator", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new SuggestionItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Kind = "automated-insight",
                    Title = $"Operators often search {Describe(to)} after {Describe(from)}",
                    Detail =
                        $"Sequence support {count}/{maxPairCount} (confidence {confidence:0.00}). " +
                        "Open matching filters or that device next.",
                    RelatedDeviceId = ExtractDeviceId(to),
                    Confidence = confidence * 0.9
                });
            }
        }

        // 2) Live fault highlights from unique HashSet / recent critical alerts (pattern-linked when possible).
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
                Confidence = 0.55
            });
        }

        foreach (var alert in recentAlerts.Where(a => a.Severity == AlertSeverity.Critical).TakeLast(3))
        {
            if (TryBuildPatternFollowUp(alert, maxPairCount, out var followUp) && followUp is not null)
            {
                suggestions.Add(followUp);
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
                    Confidence = 0.70
                });
            }
        }

        // 3) Problematic devices currently Critical/Invalid / Disconnected.
        foreach (var device in fleet
                     .Where(d => d.Health is HealthState.Critical or HealthState.Invalid
                                 || d.Freshness == FreshnessState.Disconnected)
                     .Take(5))
        {
            var confidence = device.Freshness == FreshnessState.Disconnected ? 0.60 : 0.50;
            suggestions.Add(new SuggestionItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = "automated-insight",
                Title = device.Freshness == FreshnessState.Disconnected
                    ? $"Disconnected node: {device.Id}"
                    : $"Device health {device.Health}: {device.Id}",
                Detail = device.Freshness == FreshnessState.Disconnected
                    ? $"Freshness is Disconnected (last seen {device.LastSeenAt:u}). Gateway detection raised/keeps the HashSet key."
                    : $"Category {device.Category}. Highlighted before you search — gateway-classified state.",
                RelatedDeviceId = device.Id,
                Confidence = confidence
            });
        }

        var ranked = suggestions
            .GroupBy(s => s.Title + "|" + s.RelatedDeviceId)
            .Select(g => g.OrderByDescending(s => s.Confidence).First())
            .OrderByDescending(s => s.Confidence)
            .Take(10)
            .ToList();

        return new SuggestionsResponse
        {
            Suggestions = ranked,
            SearchHistoryCount = _searches.Count,
            SequencePairCount = _sequenceCounts.Count
        };
    }

    /// <summary>
    /// Prefer a learned command follow-up for this alert's device/fault context over any fixed rule.
    /// </summary>
    private bool TryBuildPatternFollowUp(PriorityAlert alert, int maxPairCount, out SuggestionItem? item)
    {
        item = null;
        var candidates = _sequenceCounts
            .Where(kv =>
            {
                var parts = kv.Key.Split('|', 2);
                if (parts.Length != 2)
                {
                    return false;
                }

                var from = parts[0];
                var to = parts[1];
                if (!to.StartsWith("command:", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return from.Contains(alert.DeviceId, StringComparison.OrdinalIgnoreCase)
                       || from.Contains("fault:", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(kv => kv.Value)
            .ToList();

        if (candidates.Count == 0)
        {
            return false;
        }

        var best = candidates[0];
        var to = best.Key.Split('|', 2)[1];
        var commandParts = to.Split(':', 3);
        if (commandParts.Length != 3
            || !Enum.TryParse<CommandKind>(commandParts[2], ignoreCase: true, out var kind))
        {
            return false;
        }

        var confidence = NormalizeConfidence(best.Value, maxPairCount);
        item = new SuggestionItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = "suggested-action",
            Title = $"Learned follow-up for {alert.DeviceId}: {kind}",
            Detail =
                $"{alert.Reason}. Pattern support {best.Value}/{maxPairCount} " +
                $"(confidence {confidence:0.00}) links prior fault/search context to {commandParts[1]}.",
            RelatedDeviceId = commandParts[1],
            SuggestedCommand = kind,
            Confidence = Math.Min(0.95, confidence + 0.05)
        };
        return true;
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

        // Device-scoped keys stay generic — no moisture/pump special cases.
        if (!string.IsNullOrWhiteSpace(device))
        {
            return $"search:device:{device}";
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

    private static double NormalizeConfidence(int count, int maxCount)
    {
        if (maxCount <= 0 || count <= 0)
        {
            return 0;
        }

        return Math.Clamp(count / (double)maxCount, 0.05, 1.0);
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
