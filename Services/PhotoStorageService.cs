namespace Spotster.Services;

using Spotster.Infrastructure.Storage;
using Spotster.Resources;
using Microsoft.Extensions.Localization;

public class PhotoStorageService : IPhotoStorageService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    private readonly IBlobStorage _blobStorage;
    private readonly ILogger<PhotoStorageService> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public PhotoStorageService(
        IBlobStorage blobStorage,
        ILogger<PhotoStorageService> logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _blobStorage = blobStorage;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<string?> SaveParkingPhotoAsync(IFormFile? photo, Guid reportId)
    {
        if (photo is null || photo.Length == 0)
        {
            return null;
        }

        ValidatePhoto(photo);

        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        var relativePath = $"uploads/parking/{reportId}{extension}";
        await using var stream = photo.OpenReadStream();
        var photoUrl = await _blobStorage.SaveAsync(relativePath, stream, photo.ContentType);
        _logger.LogInformation("Photo saved for report {ReportId}: {PhotoUrl}", reportId, photoUrl);
        return photoUrl;
    }

    public async Task<string> SaveProfilePhotoAsync(IFormFile photo, Guid userId)
    {
        ValidatePhotoRequired(photo);
        ValidatePhoto(photo);

        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        var relativePath = $"uploads/profiles/{userId}{extension}";
        await using var stream = photo.OpenReadStream();
        var photoUrl = await _blobStorage.SaveAsync(relativePath, stream, photo.ContentType);
        _logger.LogInformation("Profile photo saved for user {UserId}: {PhotoUrl}", userId, photoUrl);
        return photoUrl;
    }

    public async Task<string> SaveChatPhotoAsync(IFormFile photo, Guid messageId)
    {
        ValidatePhotoRequired(photo);
        ValidatePhoto(photo);

        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        var relativePath = $"uploads/chat/{messageId}{extension}";
        await using var stream = photo.OpenReadStream();
        var photoUrl = await _blobStorage.SaveAsync(relativePath, stream, photo.ContentType);
        _logger.LogInformation("Chat photo saved for message {MessageId}: {PhotoUrl}", messageId, photoUrl);
        return photoUrl;
    }

    public void DeletePhoto(string? photoUrl)
    {
        _ = _blobStorage.DeleteAsync(photoUrl ?? string.Empty);
    }

    private void ValidatePhotoRequired(IFormFile? photo)
    {
        if (photo is null || photo.Length == 0)
        {
            throw new ArgumentException(_localizer["Error_PhotoRequired"]);
        }
    }

    private void ValidatePhoto(IFormFile photo)
    {
        if (photo.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException(_localizer["Error_PhotoTooLarge"]);
        }

        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException(_localizer["Error_PhotoUnsupportedFormat"]);
        }

        if (!string.IsNullOrWhiteSpace(photo.ContentType) &&
            !AllowedContentTypes.Contains(photo.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(_localizer["Error_PhotoInvalidType"]);
        }
    }
}
