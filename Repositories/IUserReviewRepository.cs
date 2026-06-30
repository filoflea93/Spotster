using Spotster.Entities;

namespace Spotster.Repositories;

public class SharedRequestInteraction
{
    public Guid ParkingRequestId { get; set; }
    public string Address { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public interface IUserReviewRepository
{
    Task<UserReview?> GetByIdAsync(Guid id);
    Task<UserReview?> GetExistingBetweenUsersAsync(Guid reviewerUserId, Guid reviewedUserId);
    Task<(List<UserReview> Items, int Total)> GetByReviewedUserAsync(Guid reviewedUserId, int page, int pageSize);
    Task<(int TotalStars, int TotalCount)> GetSummaryAsync(Guid reviewedUserId);
    Task<List<SharedRequestInteraction>> GetSharedInteractionsAsync(Guid userA, Guid userB);
    Task AddAsync(UserReview review);
    Task SaveChangesAsync();
}
