using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

/// <summary>
/// Receives heterogeneous ESP32 telemetry as closed generics so JSON never deserializes
/// mixed readings into object (no boxing). Actions are async so the dashboard never blocks the API thread.
/// </summary>
[ApiController]
[Route("api/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private readonly GatewayStore _store;

    public TelemetryController(GatewayStore store)
    {
        _store = store;
    }

    [HttpPost("environmental")]
    public Task<ActionResult<IngestTelemetryResponse>> IngestEnvironmental(
        [FromBody] IngestTelemetryRequest<EnvironmentalReading> request,
        CancellationToken cancellationToken)
        => IngestAsync(request.Packet, _store.TryIngestEnvironmentalAsync, TelemetryPacketValidator.Validate, cancellationToken);

    [HttpPost("power")]
    public Task<ActionResult<IngestTelemetryResponse>> IngestPower(
        [FromBody] IngestTelemetryRequest<PowerReading> request,
        CancellationToken cancellationToken)
        => IngestAsync(request.Packet, _store.TryIngestPowerAsync, TelemetryPacketValidator.Validate, cancellationToken);

    [HttpPost("actuator")]
    public Task<ActionResult<IngestTelemetryResponse>> IngestActuator(
        [FromBody] IngestTelemetryRequest<ActuatorReading> request,
        CancellationToken cancellationToken)
        => IngestAsync(request.Packet, _store.TryIngestActuatorAsync, TelemetryPacketValidator.Validate, cancellationToken);

    private async Task<ActionResult<IngestTelemetryResponse>> IngestAsync<T>(
        TelemetryPacket<T>? packet,
        TryIngestAsync<T> tryIngest,
        Func<TelemetryPacket<T>, SensorDevice?, ValidationResult> validate,
        CancellationToken cancellationToken)
        where T : struct
    {
        if (packet is null)
        {
            await _store.RecordRejectionAsync(string.Empty, ["Telemetry packet is required."], cancellationToken);
            return BadRequest(Fail(string.Empty, ["Telemetry packet is required."]));
        }

        var device = string.IsNullOrWhiteSpace(packet.DeviceId)
            ? null
            : await _store.FindSensorAsync(packet.DeviceId, cancellationToken);
        var validation = validate(packet, device);
        if (!validation.IsValid)
        {
            await _store.RecordRejectionAsync(packet.DeviceId, validation.Errors, cancellationToken);
            var status = device is null && !string.IsNullOrWhiteSpace(packet.DeviceId)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return StatusCode(status, Fail(packet.DeviceId, validation.Errors));
        }

        var ingested = await tryIngest(packet, cancellationToken);
        if (!ingested.Succeeded && ingested.Error is not null)
        {
            return BadRequest(Fail(packet.DeviceId, [ingested.Error]));
        }

        var updated = await _store.FindSensorAsync(packet.DeviceId, cancellationToken);
        return Ok(new IngestTelemetryResponse
        {
            Succeeded = true,
            DeviceId = packet.DeviceId,
            Health = updated?.Health,
            Freshness = updated?.Freshness
        });
    }

    private static IngestTelemetryResponse Fail(string deviceId, IEnumerable<string> errors)
    {
        return new IngestTelemetryResponse
        {
            Succeeded = false,
            DeviceId = deviceId,
            Errors = [.. errors]
        };
    }

    private delegate Task<StoreResult> TryIngestAsync<T>(
        TelemetryPacket<T> packet,
        CancellationToken cancellationToken)
        where T : struct;
}
