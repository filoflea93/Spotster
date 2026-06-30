using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Spotster.Infrastructure.Storage;

public class AzureBlobStorage : IBlobStorage
{
    private readonly BlobStorageSettings _settings;
    private readonly ILogger<AzureBlobStorage> _logger;
    private readonly BlobContainerClient _container;

    public AzureBlobStorage(IOptions<BlobStorageSettings> settings, ILogger<AzureBlobStorage> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        var service = new BlobServiceClient(_settings.ConnectionString);
        _container = service.GetBlobContainerClient(_settings.ContainerName);
        _container.CreateIfNotExists(PublicAccessType.Blob);
    }

    public async Task<string> SaveAsync(string relativePath, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var blobName = relativePath.TrimStart('/').Replace('\\', '/');
        var blob = _container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);

        var baseUrl = (_settings.PublicBaseUrl ?? _container.Uri.ToString()).TrimEnd('/');
        var publicUrl = $"{baseUrl}/{blobName}";
        _logger.LogDebug("Blob saved to Azure: {Url}", publicUrl);
        return publicUrl;
    }

    public async Task DeleteAsync(string publicUrlOrPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicUrlOrPath))
        {
            return;
        }

        var blobName = ExtractBlobName(publicUrlOrPath);
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return;
        }

        await _container.DeleteBlobIfExistsAsync(blobName, cancellationToken: cancellationToken);
        _logger.LogDebug("Blob deleted from Azure: {Blob}", blobName);
    }

    private string ExtractBlobName(string publicUrlOrPath)
    {
        if (publicUrlOrPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(publicUrlOrPath);
            return uri.AbsolutePath.TrimStart('/');
        }

        return publicUrlOrPath.TrimStart('/');
    }
}
