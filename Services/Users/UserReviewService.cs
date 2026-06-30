using Spotster.DTOs;
using Spotster.Entities;
using Spotster.Repositories;
using Spotster.Resources;
using Microsoft.Extensions.Localization;

namespace Spotster.Services.Users;

public interface IUserReviewService
{
    Task<IReadOnlyList<UserSearchResultDto>> SearchUsersAsync(Guid currentUserId, string query, int limit);
    Task<UserReviewSummaryDto> GetSummaryAsync(Guid reviewedUserId);
    Task<PagedResult<UserReviewDto>> GetReviewsAsync(Guid reviewedUserId, Guid? currentUserId, int page, int pageSize);
    Task<UserReviewStatusDto> GetReviewStatusAsync(Guid reviewerUserId, Guid reviewedUserId);
    Task<UserReviewDto> CreateReviewAsync(Guid reviewerUserId, Guid reviewedUserId, CreateUserReviewRequest request);
}

public class UserReviewService : IUserReviewService
{
    private const int MaxCommentLength = 500;
    private const int MinSearchLength = 2;

    private readonly IUserRepository _users;
    private readonly IUserReviewRepository _reviews;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public UserReviewService(
        IUserRepository users,
        IUserReviewRepository reviews,
        IStringLocalizer<SharedResources> localizer)
    {
        _users = users;
        _reviews = reviews;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<UserSearchResultDto>> SearchUsersAsync(Guid currentUserId, string query, int limit)
    {
        query = query?.Trim() ?? string.Empty;
        if (query.Length < MinSearchLength)
        {
            return [];
        }

        limit = Math.Clamp(limit, 1, 20);
        var users = await _users.SearchByUsernameAsync(query, currentUserId, limit);
        return users.Select(u => new UserSearchResultDto(u.Id, u.UserName ?? string.Empty)).ToList();
    }

    public async Task<UserReviewSummaryDto> GetSummaryAsync(Guid reviewedUserId)
    {
        var user = await _users.GetByIdAsync(reviewedUserId);
        var (totalStars, count) = await _reviews.GetSummaryAsync(reviewedUserId);
        return new UserReviewSummaryDto(totalStars, count, user?.ProfilePhotoUrl);
    }

    public async Task<PagedResult<UserReviewDto>> GetReviewsAsync(
        Guid reviewedUserId,
        Guid? currentUserId,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var (items, total) = await _reviews.GetByReviewedUserAsync(reviewedUserId, page, pageSize);
        var dtos = items.Select(r => ToDto(r, currentUserId)).ToList();
        return new PagedResult<UserReviewDto>(dtos, page, pageSize, total);
    }

    public async Task<UserReviewStatusDto> GetReviewStatusAsync(Guid reviewerUserId, Guid reviewedUserId)
    {
        if (reviewerUserId == reviewedUserId)
        {
            return new UserReviewStatusDto(false, false);
        }

        var existing = await _reviews.GetExistingBetweenUsersAsync(reviewerUserId, reviewedUserId);
        if (existing is not null)
        {
            return new UserReviewStatusDto(true, false);
        }

        var interactions = await _reviews.GetSharedInteractionsAsync(reviewerUserId, reviewedUserId);
        return new UserReviewStatusDto(false, interactions.Count > 0);
    }

    public async Task<UserReviewDto> CreateReviewAsync(
        Guid reviewerUserId,
        Guid reviewedUserId,
        CreateUserReviewRequest request)
    {
        if (reviewerUserId == reviewedUserId)
        {
            throw new InvalidOperationException(_localizer["Error_CannotReviewSelf"]);
        }

        if (request.Rating is < 1 or > 5)
        {
            throw new ArgumentException(_localizer["Error_InvalidReviewRating"]);
        }

        var comment = request.Comment?.Trim();
        if (comment?.Length > MaxCommentLength)
        {
            throw new ArgumentException(_localizer["Error_ReviewCommentTooLong", MaxCommentLength]);
        }

        var reviewedUser = await _users.GetByIdAsync(reviewedUserId)
            ?? throw new KeyNotFoundException(_localizer["Error_UserNotFound"]);

        var existing = await _reviews.GetExistingBetweenUsersAsync(reviewerUserId, reviewedUserId);
        if (existing is not null)
        {
            throw new InvalidOperationException(_localizer["Error_ReviewAlreadyExists"]);
        }

        var interactions = await _reviews.GetSharedInteractionsAsync(reviewerUserId, reviewedUserId);
        if (interactions.Count == 0)
        {
            throw new InvalidOperationException(_localizer["Error_ReviewNotEligible"]);
        }

        Guid? parkingRequestId = interactions[0].ParkingRequestId;
        var parkingRequest = new ParkingRequest
        {
            Id = parkingRequestId.Value,
            Address = interactions[0].Address
        };

        var review = new UserReview
        {
            Id = Guid.NewGuid(),
            ReviewerUserId = reviewerUserId,
            ReviewedUserId = reviewedUserId,
            ParkingRequestId = parkingRequestId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment,
            CreatedAt = DateTime.UtcNow
        };

        review.ReviewerUser = await _users.GetByIdAsync(reviewerUserId) ?? new User { UserName = "user" };
        review.ReviewedUser = reviewedUser;
        review.ParkingRequest = parkingRequest;

        await _reviews.AddAsync(review);
        await _reviews.SaveChangesAsync();

        return ToDto(review, reviewerUserId);
    }

    private static UserReviewDto ToDto(UserReview review, Guid? currentUserId) =>
        new(
            review.Id,
            review.ReviewerUserId,
            review.ReviewerUser?.UserName ?? "user",
            review.ReviewedUserId,
            review.ParkingRequestId,
            review.ParkingRequest?.Address,
            review.Rating,
            review.Comment,
            review.CreatedAt,
            currentUserId.HasValue && review.ReviewerUserId == currentUserId.Value);
}
