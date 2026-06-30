namespace Spotster.Services;

public interface IPhotoStorageService
{
    Task<string?> SaveParkingPhotoAsync(IFormFile? photo, Guid reportId);
    Task<string> SaveProfilePhotoAsync(IFormFile photo, Guid userId);
    Task<string> SaveChatPhotoAsync(IFormFile photo, Guid messageId);
    void DeletePhoto(string? photoUrl);
}
