namespace Spotster.Infrastructure.Storage;

public class BlobStorageSettings
{
    public const string SectionName = "BlobStorage";

    /// <summary>Azure Blob connection string. When empty, files are stored on local disk.</summary>
    public string? ConnectionString { get; set; }

    public string ContainerName { get; set; } = "spotster";

    /// <summary>Public base URL for blobs (CDN or blob endpoint). Used when uploading to Azure.</summary>
    public string? PublicBaseUrl { get; set; }

    public bool UseAzure => !string.IsNullOrWhiteSpace(ConnectionString);
}
