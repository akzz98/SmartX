namespace SmartX.Shared;

/// <summary>
/// Maps priority alerts and manual overrides onto command-stream DTOs.
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

    public static CommandResponse ToCommandResponse(DeviceCommand command)
    {
        return new CommandResponse
        {
            Id = command.Id,
            DeviceId = command.DeviceId,
            MacAddress = command.MacAddress,
            Kind = command.Kind,
            TargetIsActive = command.TargetIsActive,
            PreviousIsActive = command.PreviousIsActive,
            IssuedAt = command.IssuedAt,
            OperatorNote = command.OperatorNote
        };
    }

    public static TimelineEntryResponse ToTimelineEntryResponse(
        string kind,
        DateTimeOffset timestamp,
        string deviceId,
        string macAddress,
        string summary,
        DeviceCommand? command,
        PriorityAlert? alert)
    {
        return new TimelineEntryResponse
        {
            Kind = kind,
            Timestamp = timestamp,
            DeviceId = deviceId,
            MacAddress = macAddress,
            Summary = summary,
            Command = command is null ? null : ToCommandResponse(command),
            Alert = alert is null ? null : ToAlertResponse(alert)
        };
    }

    /// <summary>
    /// Resolves the commanded open/on flag for an override kind.
    /// ToggleRelay flips the previous expected position.
    /// </summary>
    public static bool ResolveTargetIsActive(CommandKind kind, bool? previousIsActive)
    {
        return kind switch
        {
            CommandKind.OpenValve or CommandKind.StartPump => true,
            CommandKind.CloseValve or CommandKind.StopPump => false,
            CommandKind.ToggleRelay => previousIsActive != true,
            _ => previousIsActive == true
        };
    }
}

