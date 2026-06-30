namespace Spotster.Infrastructure.Storage;

public class LocalBlobStorage : IBlobStorage
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LocalBlobStorage> _logger;

    public LocalBlobStorage(IWebHostEnvironment environment, ILogger<LocalBlobStorage> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SaveAsync(string relativePath, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var normalized = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_environment.WebRootPath, normalized);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = new FileStream(fullPath, FileMode.Create);
        await content.CopyToAsync(fileStream, cancellationToken);

        var publicUrl = "/" + relativePath.TrimStart('/').Replace('\\', '/');
        _logger.LogDebug("Blob saved locally: {Url}", publicUrl);
        return publicUrl;
    }

    public Task DeleteAsync(string publicUrlOrPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicUrlOrPath) || publicUrlOrPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var relativePath = publicUrlOrPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_environment.WebRootPath, relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogDebug("Blob deleted locally: {Url}", publicUrlOrPath);
        }

        return Task.CompletedTask;
    }
}
