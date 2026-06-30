namespace Spotster.Infrastructure.Storage;

public interface IBlobStorage
{
    Task<string> SaveAsync(string relativePath, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task DeleteAsync(string publicUrlOrPath, CancellationToken cancellationToken = default);
}
