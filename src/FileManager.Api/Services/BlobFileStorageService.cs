using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FileManager.Api.Options;
using Microsoft.Extensions.Options;

namespace FileManager.Api.Services;

public class BlobFileStorageService : IFileStorageService
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<BlobFileStorageService> _logger;
    private bool _containerReady;

    public BlobFileStorageService(IOptions<StorageOptions> options, ILogger<BlobFileStorageService> logger)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ConnectionString))
            throw new InvalidOperationException("Storage:ConnectionString is not configured.");

        _container = new BlobContainerClient(o.ConnectionString, o.ContainerName);
        _logger = logger;
    }

    public async Task UploadAsync(string name, Stream content, string contentType, CancellationToken ct)
    {
        var blobName = Sanitize(name);
        try
        {
            await EnsureContainerAsync(ct);
            var blob = _container.GetBlobClient(blobName);
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            }, ct); // overwrites by default when no conditions are set

            _logger.LogInformation("Uploaded file {FileName} ({Size} bytes, {ContentType})",
                blobName, content.CanSeek ? content.Length : -1, contentType);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Upload failed for {FileName}", blobName);
            throw;
        }
    }

    public async Task<IReadOnlyList<FileItem>> ListAsync(CancellationToken ct)
    {
        try
        {
            await EnsureContainerAsync(ct);
            var items = new List<FileItem>();
            await foreach (var blob in _container.GetBlobsAsync(cancellationToken: ct))
            {
                items.Add(new FileItem(blob.Name, blob.Properties.ContentLength ?? 0, blob.Properties.LastModified));
            }
            _logger.LogInformation("Listed {Count} files", items.Count);
            return items;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Listing files failed");
            throw;
        }
    }

    public async Task<FileDownload?> DownloadAsync(string name, CancellationToken ct)
    {
        var blobName = Sanitize(name);
        try
        {
            var result = await _container.GetBlobClient(blobName).DownloadStreamingAsync(cancellationToken: ct);
            var contentType = result.Value.Details.ContentType ?? "application/octet-stream";
            _logger.LogInformation("Downloading file {FileName} ({Size} bytes)", blobName, result.Value.Details.ContentLength);
            return new FileDownload(result.Value.Content, contentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("File {FileName} not found", blobName);
            return null;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Download failed for {FileName}", blobName);
            throw;
        }
    }

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_containerReady) return;
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
        _containerReady = true;
    }

    private static string Sanitize(string name)
    {
        var fileName = Path.GetFileName(name ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Invalid file name.", nameof(name));
        return fileName;
    }
}
