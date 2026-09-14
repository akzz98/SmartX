using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

/// <summary>
/// Sensor registration and Progressive Disclosure reads (overview, filter, detail, history).
/// All actions are async so Blazor HttpClient calls do not block the gateway thread.
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
    public async Task<ActionResult<RegisterSensorResponse>> Register(
        [FromBody] RegisterSensorRequest request,
        CancellationToken cancellationToken)
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
            LastSeenAt = null,
            Health = HealthState.Normal,
            Freshness = FreshnessState.Disconnected
        };

        var added = await _store.TryAddAsync(device, cancellationToken);
        if (!added.Succeeded && added.Error is not null)
        {
            var failed = new ValidationResult();
            failed.AddError(added.Error);
            return Conflict(SensorDtoMapper.ToFailedRegistration(failed));
        }

        return Ok(SensorDtoMapper.ToSucceededRegistration(device));
    }

    /// <summary>
    /// Level 0 overview: counts and roll-ups so operators can see what needs attention.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<FleetSummaryResponse>> Summary(CancellationToken cancellationToken)
    {
        var sensors = await _store.SnapshotSensorsAsync(cancellationToken);
        var powerPackets = await _store.SnapshotPowerPacketsAsync(cancellationToken);
        var combinedWatts = PowerLoadAggregator.CombinedLatestWatts(powerPackets);
        var wattDelta = PowerLoadAggregator.LatestWattDelta(powerPackets);
        var summary = new FleetSummaryResponse
        {
            DeviceCount = sensors.Count,
            ExceptionCount = sensors.Count(sensor =>
                sensor.Health is HealthState.Warning or HealthState.Critical or HealthState.Invalid),
            StaleOrDisconnectedCount = sensors.Count(sensor =>
                sensor.Freshness is FreshnessState.Stale or FreshnessState.Disconnected),
            CombinedSiteWatts = combinedWatts.Value,
            LatestWattDelta = wattDelta?.Value
        };

        CountBy(summary.ByCategory, sensors, sensor => sensor.Category.ToString());
        CountBy(summary.ByLocation, sensors, sensor => sensor.LocationNodeId);
        CountBy(summary.ByHealth, sensors, sensor => sensor.Health.ToString());
        CountBy(summary.ByFreshness, sensors, sensor => sensor.Freshness.ToString());
        return Ok(summary);
    }

    /// <summary>
    /// Level 1 zoom/filter: which subset of the fleet is affected.
    /// A location id includes that node and every descendant (zone filter includes racks).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SensorListResponse>> List(
        [FromQuery] string? locationNodeId,
        [FromQuery] SensorCategory? category,
        [FromQuery] string? id,
        [FromQuery] string? mac,
        [FromQuery] HealthState? health,
        [FromQuery] FreshnessState? freshness,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        IEnumerable<SensorDevice> sensors = await _store.SnapshotSensorsAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(locationNodeId))
        {
            var location = DeploymentTree.Find(_store.DeploymentRoots, locationNodeId);
            var locationIds = location is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { locationNodeId }
                : DeploymentTree.SelfAndDescendantIds(location);
            sensors = sensors.Where(sensor => locationIds.Contains(sensor.LocationNodeId));
        }

        if (category is not null)
        {
            sensors = sensors.Where(sensor => sensor.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(id))
        {
            sensors = sensors.Where(sensor =>
                sensor.Id.Contains(id, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(mac))
        {
            sensors = sensors.Where(sensor =>
                sensor.MacAddress.Contains(mac, StringComparison.OrdinalIgnoreCase));
        }

        if (health is not null)
        {
            sensors = sensors.Where(sensor => sensor.Health == health);
        }

        if (freshness is not null)
        {
            sensors = sensors.Where(sensor => sensor.Freshness == freshness);
        }

        if (from is not null)
        {
            sensors = sensors.Where(sensor => sensor.LastSeenAt is { } seen && seen >= from);
        }

        if (to is not null)
        {
            sensors = sensors.Where(sensor => sensor.LastSeenAt is { } seen && seen <= to);
        }

        return Ok(SensorDtoMapper.ToListResponse(sensors));
    }

    /// <summary>
    /// Level 2 details: what happened on this device.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<DeviceDetailResponse>> Get(string id, CancellationToken cancellationToken)
    {
        var device = await _store.FindSensorAsync(id, cancellationToken);
        if (device is null)
        {
            return NotFound();
        }

        var location = DeploymentTree.Find(_store.DeploymentRoots, device.LocationNodeId);
        var environmental = await _store.SnapshotEnvironmentalPacketsAsync(device.Id, cancellationToken);
        return Ok(new DeviceDetailResponse
        {
            Sensor = SensorDtoMapper.ToResponse(device),
            LocationName = location?.Name,
            LocationLevel = location?.Level,
            LatestEnvironmentalDelta = EnvironmentalDelta.Latest(environmental)
        });
    }

    /// <summary>
    /// Level 3 history: why it happened — latest typed packets for this device.
    /// </summary>
    [HttpGet("{id}/history")]
    public async Task<ActionResult<TelemetryHistoryResponse>> History(
        string id,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var device = await _store.FindSensorAsync(id, cancellationToken);
        if (device is null)
        {
            return NotFound();
        }

        take = Math.Clamp(take, 1, 500);
        return Ok(await _store.HistoryForAsync(device, take, cancellationToken));
    }

    private static void CountBy(
        Dictionary<string, int> target,
        IReadOnlyList<SensorDevice> sensors,
        Func<SensorDevice, string> key)
    {
        foreach (var group in sensors.GroupBy(key))
        {
            target[group.Key] = group.Count();
        }
    }

    // Store MACs as AA:BB:CC:DD:EE:FF so duplicate checks and the dashboard see one format.
    private static string NormalizeMac(string mac)
    {
        var hex = new string([.. mac.Where(char.IsAsciiHexDigit)]);
        return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2).ToUpperInvariant()));
    }
}
