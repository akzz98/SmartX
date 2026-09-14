using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

/// <summary>
/// Sensor registration for the Smart-X gateway. Fleet queries come in a later Stage 4 item.
/// </summary>
[ApiController]
[Route("api/sensors")]
public sealed class SensorsController : ControllerBase
{
    private readonly GatewayStore _store;

    public SensorsController(GatewayStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Registers a device (MAC, location node, category). Validation uses the shared
    /// SensorRegistrationValidator against the in-memory deployment tree.
    /// </summary>
    [HttpPost]
    public ActionResult<RegisterSensorResponse> Register([FromBody] RegisterSensorRequest request)
    {
        var validation = SensorRegistrationValidator.Validate(
            request.ToRegistration(),
            _store.DeploymentRoots);

        if (!validation.IsValid)
        {
            return BadRequest(SensorDtoMapper.ToFailedRegistration(validation));
        }

        var now = DateTimeOffset.UtcNow;
        var mac = NormalizeMac(request.MacAddress);
        var device = new SensorDevice
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? $"sx-{mac.Replace(":", string.Empty)}" : request.Id.Trim(),
            MacAddress = mac,
            LocationNodeId = request.LocationNodeId.Trim(),
            Category = request.Category,
            RegisteredAt = now,
            LastSeenAt = null
        };

        if (!_store.TryAdd(device, out var duplicateError) && duplicateError is not null)
        {
            var failed = new ValidationResult();
            failed.AddError(duplicateError);
            return Conflict(SensorDtoMapper.ToFailedRegistration(failed));
        }

        return Ok(SensorDtoMapper.ToSucceededRegistration(device));
    }

    // Store MACs as AA:BB:CC:DD:EE:FF so duplicate checks and the dashboard see one format.
    private static string NormalizeMac(string mac)
    {
        var hex = new string([.. mac.Where(char.IsAsciiHexDigit)]);
        return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2).ToUpperInvariant()));
    }
}
