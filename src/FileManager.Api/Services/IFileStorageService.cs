namespace FileManager.Api.Services;

public record FileItem(string Name, long Size, DateTimeOffset? LastModified);

public record FileDownload(Stream Content, string ContentType);

public interface IFileStorageService
{
    Task UploadAsync(string name, Stream content, string contentType, CancellationToken ct);
    Task<IReadOnlyList<FileItem>> ListAsync(CancellationToken ct);
    Task<FileDownload?> DownloadAsync(string name, CancellationToken ct);
}
