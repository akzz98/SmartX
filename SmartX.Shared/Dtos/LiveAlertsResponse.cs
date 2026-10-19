namespace SmartX.Shared;

/// <summary>
/// Currently active unique alerts (backed by HashSet tracking on the API in a later stage).
/// </summary>
public sealed class LiveAlertsResponse
{
    public List<AlertResponse> Alerts { get; set; } = [];
}
