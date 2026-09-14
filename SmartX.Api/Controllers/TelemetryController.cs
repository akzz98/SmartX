using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

/// <summary>
/// Receives heterogeneous ESP32 telemetry as closed generics so JSON never deserializes
/// mixed readings into object (no boxing). Range/health/freshness classification is next.
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
        => Ingest(request.Packet, _store.TryIngestEnvironmental);

    [HttpPost("power")]
    public ActionResult<IngestTelemetryResponse> IngestPower(
        [FromBody] IngestTelemetryRequest<PowerReading> request)
        => Ingest(request.Packet, _store.TryIngestPower);

    [HttpPost("actuator")]
    public ActionResult<IngestTelemetryResponse> IngestActuator(
        [FromBody] IngestTelemetryRequest<ActuatorReading> request)
        => Ingest(request.Packet, _store.TryIngestActuator);

    private ActionResult<IngestTelemetryResponse> Ingest<T>(
        TelemetryPacket<T>? packet,
        TryIngest<T> tryIngest)
        where T : struct
    {
        if (packet is null)
        {
            return BadRequest(Fail(string.Empty, "Telemetry packet is required."));
        }

        if (!tryIngest(packet, out var error) && error is not null)
        {
            var status = error.Contains("not registered", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return StatusCode(status, Fail(packet.DeviceId, error));
        }

        // Health and freshness stay unset until the Stage 4 classification items.
        return Ok(new IngestTelemetryResponse
        {
            Succeeded = true,
            DeviceId = packet.DeviceId
        });
    }

    private static IngestTelemetryResponse Fail(string deviceId, string error)
    {
        return new IngestTelemetryResponse
        {
            Succeeded = false,
            DeviceId = deviceId,
            Errors = [error]
        };
    }

    private delegate bool TryIngest<T>(TelemetryPacket<T> packet, out string? error) where T : struct;
}
