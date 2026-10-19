using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

/// <summary>
/// Manual actuator overrides for the command stream. Issue pushes onto the undo stack.
/// </summary>
[ApiController]
[Route("api/commands")]
public sealed class CommandsController : ControllerBase
{
    private readonly GatewayStore _store;

    public CommandsController(GatewayStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Issues a manual override: push onto <see cref="CommandUndoStack"/> and apply ExpectedIsActive.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<IssueCommandResponse>> Issue(
        [FromBody] IssueCommandRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _store.TryIssueCommandAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
