namespace SmartX.Shared;

/// <summary>
/// Dashboard row for a sensor attachment. Download uses the API file endpoint, not this DTO.
/// </summary>
public sealed class AttachmentResponse
{
    public string Id { get; set; } = string.Empty;

    public string SensorId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public AttachmentKind Kind { get; set; }

    public long SizeBytes { get; set; }

    public DateTimeOffset StoredAt { get; set; }
}
