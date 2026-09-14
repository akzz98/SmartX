using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

/// <summary>
/// Receives heterogeneous ESP32 telemetry as closed generics so JSON never deserializes
/// mixed readings into object (no boxing). Packets are range-checked before they are stored.
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
    public ActionResult<IngestTelemetryResponse> IngestEnvironmental(
        [FromBody] IngestTelemetryRequest<EnvironmentalReading> request)
        => Ingest(request.Packet, _store.TryIngestEnvironmental, TelemetryPacketValidator.Validate);

    [HttpPost("power")]
    public ActionResult<IngestTelemetryResponse> IngestPower(
        [FromBody] IngestTelemetryRequest<PowerReading> request)
        => Ingest(request.Packet, _store.TryIngestPower, TelemetryPacketValidator.Validate);

    [HttpPost("actuator")]
    public ActionResult<IngestTelemetryResponse> IngestActuator(
        [FromBody] IngestTelemetryRequest<ActuatorReading> request)
        => Ingest(request.Packet, _store.TryIngestActuator, TelemetryPacketValidator.Validate);

    private ActionResult<IngestTelemetryResponse> Ingest<T>(
        TelemetryPacket<T>? packet,
        TryIngest<T> tryIngest,
        Func<TelemetryPacket<T>, SensorDevice?, ValidationResult> validate)
        where T : struct
    {
        if (packet is null)
        {
            return BadRequest(Fail(string.Empty, ["Telemetry packet is required."]));
        }

        var device = string.IsNullOrWhiteSpace(packet.DeviceId) ? null : _store.FindSensor(packet.DeviceId);
        var validation = validate(packet, device);
        if (!validation.IsValid)
        {
            var status = device is null && !string.IsNullOrWhiteSpace(packet.DeviceId)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return StatusCode(status, Fail(packet.DeviceId, validation.Errors));
        }

        if (!tryIngest(packet, out var error) && error is not null)
        {
            return BadRequest(Fail(packet.DeviceId, [error]));
        }

        // Health and freshness stay unset on the response until the classification items.
        return Ok(new IngestTelemetryResponse
        {
            Succeeded = true,
            DeviceId = packet.DeviceId
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

    private delegate bool TryIngest<T>(TelemetryPacket<T> packet, out string? error) where T : struct;
}
