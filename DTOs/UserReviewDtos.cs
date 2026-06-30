namespace Spotster.DTOs;

public record UserSearchResultDto(Guid UserId, string Username);

public record UserReviewDto(
    Guid Id,
    Guid ReviewerUserId,
    string ReviewerUsername,
    Guid ReviewedUserId,
    Guid? ParkingRequestId,
    string? ParkingRequestAddress,
    byte Rating,
    string? Comment,
    DateTime CreatedAt,
    bool IsMine);

public record UserReviewSummaryDto(int TotalStars, int TotalCount, string? ProfilePhotoUrl);

public record UserReviewStatusDto(bool AlreadyRated, bool CanRate);

public record CreateUserReviewRequest(byte Rating, string? Comment, Guid? ParkingRequestId = null);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ProfilePhotoResponse(string ProfilePhotoUrl);
