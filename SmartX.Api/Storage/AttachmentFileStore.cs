namespace SmartX.Api.Storage;

/// <summary>
/// Bytes for rack photos, JSON configs and hardware logs live on disk so typed
/// telemetry buffers stay unboxed packets, not file payloads.
/// </summary>
public sealed class AttachmentFileStore
{
    private readonly string _root;

    public AttachmentFileStore(IWebHostEnvironment environment)
    {
        _root = Path.Combine(environment.ContentRootPath, "App_Data", "attachments");
    }

    public async Task SaveAsync(string storageId, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        var path = PathFor(storageId);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Stream OpenRead(string storageId)
    {
        return new FileStream(PathFor(storageId), FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public void TryDelete(string storageId)
    {
        var path = PathFor(storageId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string PathFor(string storageId)
    {
        // Guid-only names: never use the operator-supplied file name as a path segment.
        if (storageId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || storageId.Contains("..", StringComparison.Ordinal)
            || storageId.Contains(Path.DirectorySeparatorChar)
            || storageId.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException("Attachment storage id is not a safe file name.");
        }

        return Path.Combine(_root, storageId);
    }
}
