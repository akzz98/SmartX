namespace SmartX.Shared;

/// <summary>
/// POST body when the command-stream UI applies filters — feeds the predictive engine.
/// </summary>
public sealed class RecordSearchRequest
{
    public string? CategoryFilter { get; set; }

    public string? HealthFilter { get; set; }

    public string? AlertScope { get; set; }

    public string? RelatedDeviceId { get; set; }
}
