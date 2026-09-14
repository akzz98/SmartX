using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Storage;
using SmartX.Shared;

namespace SmartX.Api.Controllers;

/// <summary>
/// Multipart diagnostic files on an ESP32 profile (rubric: media / log file upload).
/// Failures return 400/404; they must not take the ingest pipeline down.
/// </summary>
[ApiController]
[Route("api/sensors/{sensorId}/attachments")]
public sealed class AttachmentsController : ControllerBase
{
    private readonly GatewayStore _store;
    private readonly AttachmentFileStore _files;

    public AttachmentsController(GatewayStore store, AttachmentFileStore files)
    {
        _store = store;
        _files = files;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentResponse>>> List(
        string sensorId,
        CancellationToken cancellationToken)
    {
        var device = await _store.FindSensorAsync(sensorId, cancellationToken);
        if (device is null)
        {
            return NotFound();
        }

        var rows = await _store.SnapshotAttachmentsAsync(sensorId, cancellationToken);
        return Ok(rows.Select(AttachmentValidator.ToResponse).ToList());
    }

    [HttpGet("{attachmentId}")]
    public async Task<IActionResult> Download(
        string sensorId,
        string attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await _store.FindAttachmentAsync(sensorId, attachmentId, cancellationToken);
        if (attachment is null)
        {
            return NotFound();
        }

        try
        {
            var stream = _files.OpenRead(attachment.Id);
            return File(stream, attachment.ContentType, attachment.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AttachmentValidator.MaxBytes + 32_768)]
    [RequestFormLimits(MultipartBodyLengthLimit = AttachmentValidator.MaxBytes + 32_768)]
    public async Task<ActionResult<UploadAttachmentResponse>> Upload(
        string sensorId,
        [FromForm] IFormFile? file,
        [FromForm] string? kind,
        CancellationToken cancellationToken)
    {
        var device = await _store.FindSensorAsync(sensorId, cancellationToken);
        if (device is null)
        {
            return NotFound(Fail(["Device is not registered on this gateway."]));
        }

        if (file is null)
        {
            return BadRequest(Fail(["A file is required."]));
        }

        if (string.IsNullOrWhiteSpace(kind)
            || !Enum.TryParse<AttachmentKind>(kind, ignoreCase: true, out var parsedKind)
            || !Enum.IsDefined(parsedKind))
        {
            return BadRequest(Fail(["Attachment kind must be configuration, photo, or hardware log."]));
        }

        var validation = AttachmentValidator.Validate(file.FileName, file.ContentType, file.Length, parsedKind);
        if (!validation.IsValid)
        {
            return BadRequest(Fail(validation.Errors));
        }

        var attachment = new SensorAttachment
        {
            Id = Guid.NewGuid().ToString("N"),
            SensorId = device.Id,
            FileName = Path.GetFileName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            Kind = parsedKind,
            SizeBytes = file.Length,
            StoredAt = DateTimeOffset.UtcNow
        };

        try
        {
            await using var content = file.OpenReadStream();
            await _files.SaveAsync(attachment.Id, content, cancellationToken);
        }
        catch (IOException)
        {
            return BadRequest(Fail(["The file could not be stored. Try a smaller configuration, photo, or hardware log."]));
        }

        var added = await _store.TryAddAttachmentAsync(attachment, cancellationToken);
        if (!added.Succeeded)
        {
            _files.TryDelete(attachment.Id);
            return BadRequest(Fail([added.Error ?? "The attachment could not be associated with this sensor."]));
        }

        return Ok(new UploadAttachmentResponse
        {
            Succeeded = true,
            Attachment = AttachmentValidator.ToResponse(attachment)
        });
    }

    private static UploadAttachmentResponse Fail(IEnumerable<string> errors)
    {
        return new UploadAttachmentResponse
        {
            Succeeded = false,
            Errors = [.. errors]
        };
    }
}
