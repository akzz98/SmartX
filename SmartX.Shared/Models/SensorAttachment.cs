namespace SmartX.Shared;

/// <summary>
/// Metadata for a file stored against a registered sensor. Bytes stay on disk, not in telemetry buffers.
/// </summary>
public sealed class SensorAttachment
{
    public string Id { get; set; } = string.Empty;

    public string SensorId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public AttachmentKind Kind { get; set; }

    public long SizeBytes { get; set; }

    public DateTimeOffset StoredAt { get; set; }
}
