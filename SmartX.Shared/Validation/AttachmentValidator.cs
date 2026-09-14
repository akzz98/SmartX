namespace SmartX.Shared;

/// <summary>
/// Rejects empty, oversized, or mistyped diagnostic files before they land on a sensor profile.
/// </summary>
public static class AttachmentValidator
{
    public const long MaxBytes = 2_000_000;

    public static ValidationResult Validate(string? fileName, string? contentType, long length, AttachmentKind kind)
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            result.AddError("A file name is required.");
        }

        if (length <= 0)
        {
            result.AddError("The uploaded file is empty.");
        }

        if (length > MaxBytes)
        {
            result.AddError($"Attachments must be {MaxBytes / 1_000_000} MB or smaller.");
        }

        if (!Enum.IsDefined(kind))
        {
            result.AddError("Attachment kind must be configuration, photo, or hardware log.");
            return result;
        }

        var type = contentType ?? string.Empty;
        var name = fileName ?? string.Empty;
        if (!IsAllowed(kind, type, name))
        {
            result.AddError(kind switch
            {
                AttachmentKind.Photo => "Photos must be JPEG, PNG, or WebP.",
                AttachmentKind.Configuration => "Configuration must be JSON, XML, or a text file.",
                _ => "Hardware logs must be .log, .txt, or .csv."
            });
        }

        return result;
    }

    private static bool IsAllowed(AttachmentKind kind, string contentType, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var type = contentType.ToLowerInvariant();
        return kind switch
        {
            AttachmentKind.Photo => type is "image/jpeg" or "image/png" or "image/webp"
                || ext is ".jpg" or ".jpeg" or ".png" or ".webp",
            AttachmentKind.Configuration => type is "application/json" or "application/xml" or "text/xml" or "text/plain"
                || ext is ".json" or ".xml" or ".txt" or ".conf",
            AttachmentKind.HardwareLog => type is "text/plain" or "text/csv" or "application/octet-stream"
                || ext is ".log" or ".txt" or ".csv",
            _ => false
        };
    }

    public static AttachmentResponse ToResponse(SensorAttachment attachment)
    {
        return new AttachmentResponse
        {
            Id = attachment.Id,
            SensorId = attachment.SensorId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            Kind = attachment.Kind,
            SizeBytes = attachment.SizeBytes,
            StoredAt = attachment.StoredAt
        };
    }
}
