using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

/// <summary>
/// Network topology and mesh routing APIs (Final PoE Stages 4+).
/// </summary>
[ApiController]
[Route("api/mesh")]
public sealed class MeshController : ControllerBase
{
    private readonly GatewayStore _store;

    public MeshController(GatewayStore store)
    {
        _store = store;
    }

    /// <summary>
    /// GET /api/mesh/topology — snapshot of mesh vertices, undirected edges, and online status.
    /// </summary>
    [HttpGet("topology")]
    public Task<ActionResult<MeshTopologySnapshotResponse>> GetTopology(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ActionResult<MeshTopologySnapshotResponse>>(
            Ok(_store.GetMeshTopologySnapshot()));
    }

    /// <summary>
    /// GET /api/mesh/traversal?root=sx-gateway — BFS visit order, down/cut-off nodes, hop suggestions.
    /// </summary>
    [HttpGet("traversal")]
    public Task<ActionResult<MeshTraversalResponse>> GetTraversal(
        [FromQuery] string? root,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return Task.FromResult<ActionResult<MeshTraversalResponse>>(
                Ok(_store.GetMeshTraversal(root)));
        }
        catch (KeyNotFoundException ex)
        {
            return Task.FromResult<ActionResult<MeshTraversalResponse>>(NotFound(new { error = ex.Message }));
        }
    }
}
