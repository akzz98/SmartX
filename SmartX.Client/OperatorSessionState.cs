namespace SmartX.Client;

/// <summary>
/// Client-side operator session that survives Blazor page navigation.
/// Registered as a singleton for the WASM app lifetime so filters and selections
/// are not lost when moving between telemetry and the command stream
/// (Final PoE lecturer feedback: "No state survives navigation").
/// </summary>
public sealed class OperatorSessionState
{
    // --- Command stream ---

    public string CommandCategoryFilter { get; set; } = string.Empty;

    public string CommandHealthFilter { get; set; } = string.Empty;

    /// <summary>any | unique | attention</summary>
    public string CommandAlertScope { get; set; } = "any";

    public string OverrideDeviceId { get; set; } = "sx-act-rack2-valve";

    public string OverrideKindText { get; set; } = "CloseValve";

    public string OverrideNote { get; set; } = string.Empty;

    // --- Fleet (Progressive Disclosure level 1) ---

    public string FleetLocationNodeId { get; set; } = string.Empty;

    public string FleetCategoryFilter { get; set; } = string.Empty;

    public string FleetIdContains { get; set; } = string.Empty;

    public string FleetMacContains { get; set; } = string.Empty;

    public string FleetHealthFilter { get; set; } = string.Empty;

    public string FleetFreshnessFilter { get; set; } = string.Empty;

    public DateTime? FleetFromLocal { get; set; }

    public DateTime? FleetToLocal { get; set; }

    /// <summary>Last device the operator drilled into (telemetry detail).</summary>
    public string? LastDeviceId { get; set; }
}
