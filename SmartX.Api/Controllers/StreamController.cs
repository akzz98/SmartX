using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

/// <summary>
/// Real-time processing diagnostics: FIFO vs priority-queue depths and recent bypass alerts.
/// </summary>
[ApiController]
[Route("api/stream")]
public sealed class StreamController : ControllerBase
{
    private readonly GatewayStore _store;

    public StreamController(GatewayStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Snapshot for the command-stream page and README: queue depths, last processed times,
    /// and the last critical bypass reason.
    /// </summary>
    [HttpGet("snapshot")]
    public Task<ActionResult<StreamSnapshotResponse>> GetSnapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ActionResult<StreamSnapshotResponse>>(Ok(_store.GetStreamSnapshot()));
    }

    /// <summary>
    /// Processed priority alerts (newest last). Includes the seeded critical moisture bypass.
    /// </summary>
    [HttpGet("alerts")]
    public Task<ActionResult<LiveAlertsResponse>> GetAlerts(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var alerts = _store.SnapshotProcessedPriorityAlerts();
        return Task.FromResult<ActionResult<LiveAlertsResponse>>(
            Ok(StreamDtoMapper.ToLiveAlertsResponse(alerts)));
    }

    /// <summary>
    /// Currently active unique HashSet keys (disconnect/error) — no duplicate spam entries.
    /// </summary>
    [HttpGet("unique-alerts")]
    public Task<ActionResult<UniqueAlertsResponse>> GetUniqueAlerts(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ActionResult<UniqueAlertsResponse>>(Ok(_store.GetUniqueActiveAlerts()));
    }

    /// <summary>
    /// SortedDictionary timeline (oldest first) for command-stream history graphs.
    /// </summary>
    [HttpGet("timeline")]
    public Task<ActionResult<TimelineHistoryResponse>> GetTimeline(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ActionResult<TimelineHistoryResponse>>(Ok(_store.GetTimelineHistory()));
    }

    /// <summary>
    /// Records an operator filter/search so the predictive engine can learn sequences.
    /// </summary>
    [HttpPost("searches")]
    public async Task<ActionResult<OperatorSearchEvent>> RecordSearch(
        [FromBody] RecordSearchRequest request,
        CancellationToken cancellationToken)
    {
        var recorded = await _store.RecordOperatorSearchAsync(request, cancellationToken);
        return Ok(recorded);
    }

    /// <summary>
    /// Proactive Suggested Actions / Automated Insights from search+command patterns and live faults.
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<ActionResult<SuggestionsResponse>> GetSuggestions(CancellationToken cancellationToken)
    {
        var suggestions = await _store.GetSuggestionsAsync(cancellationToken);
        return Ok(suggestions);
    }
}

