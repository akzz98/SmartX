namespace SmartX.Shared;

/// <summary>
/// Maps priority alerts (and later commands) onto command-stream DTOs.
/// </summary>
public static class StreamDtoMapper
{
    public static AlertResponse ToAlertResponse(PriorityAlert alert)
    {
        return new AlertResponse
        {
            Id = alert.Id,
            DeviceId = alert.DeviceId,
            MacAddress = alert.MacAddress,
            Severity = alert.Severity,
            Reason = alert.Reason,
            RaisedAt = alert.RaisedAt,
            IsProcessed = alert.IsProcessed
        };
    }

    public static LiveAlertsResponse ToLiveAlertsResponse(IEnumerable<PriorityAlert> alerts)
    {
        return new LiveAlertsResponse
        {
            Alerts = alerts.Select(ToAlertResponse).ToList()
        };
    }
}
