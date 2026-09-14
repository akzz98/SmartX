namespace SmartX.Shared;

public sealed class UploadAttachmentResponse
{
    public bool Succeeded { get; set; }

    public AttachmentResponse? Attachment { get; set; }

    public List<string> Errors { get; set; } = [];
}
