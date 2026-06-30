using Spotster.Data;
using Spotster.Entities;
using Microsoft.EntityFrameworkCore;

namespace Spotster.Repositories;

public class UserReviewRepository : IUserReviewRepository
{
    private readonly AppDbContext _db;

    public UserReviewRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<UserReview?> GetByIdAsync(Guid id) =>
        _db.UserReviews
            .AsNoTracking()
            .Include(r => r.ReviewerUser)
            .Include(r => r.ParkingRequest)
            .FirstOrDefaultAsync(r => r.Id == id);

    public Task<UserReview?> GetExistingBetweenUsersAsync(Guid reviewerUserId, Guid reviewedUserId) =>
        _db.UserReviews.FirstOrDefaultAsync(r =>
            r.ReviewerUserId == reviewerUserId &&
            r.ReviewedUserId == reviewedUserId);

    public async Task<(List<UserReview> Items, int Total)> GetByReviewedUserAsync(
        Guid reviewedUserId,
        int page,
        int pageSize)
    {
        var query = _db.UserReviews
            .AsNoTracking()
            .Include(r => r.ReviewerUser)
            .Include(r => r.ParkingRequest)
            .Where(r => r.ReviewedUserId == reviewedUserId)
            .OrderByDescending(r => r.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(int TotalStars, int TotalCount)> GetSummaryAsync(Guid reviewedUserId)
    {
        var stats = await _db.UserReviews
            .Where(r => r.ReviewedUserId == reviewedUserId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Sum(r => (int)r.Rating),
                Count = g.Count()
            })
            .FirstOrDefaultAsync();

        if (stats is null)
        {
            return (0, 0);
        }

        return (stats.Total, stats.Count);
    }

    public async Task<List<SharedRequestInteraction>> GetSharedInteractionsAsync(Guid userA, Guid userB)
    {
        if (userA == userB)
        {
            return [];
        }

        return await _db.ParkingRequests
            .AsNoTracking()
            .Where(r =>
                r.ExpiresAt > DateTime.UtcNow &&
                (r.Status == ParkingRequestStatus.Active || r.Status == ParkingRequestStatus.Reserved) &&
                ((r.CreatedByUserId == userA &&
                  _db.ParkingRequestMessages.Any(m =>
                      m.ParkingRequestId == r.Id &&
                      m.GuestUserId == userB &&
                      m.SenderUserId == userB) &&
                  _db.ParkingRequestMessages.Any(m =>
                      m.ParkingRequestId == r.Id &&
                      m.GuestUserId == userB &&
                      m.SenderUserId == userA)) ||
                 (r.CreatedByUserId == userB &&
                  _db.ParkingRequestMessages.Any(m =>
                      m.ParkingRequestId == r.Id &&
                      m.GuestUserId == userA &&
                      m.SenderUserId == userA) &&
                  _db.ParkingRequestMessages.Any(m =>
                      m.ParkingRequestId == r.Id &&
                      m.GuestUserId == userA &&
                      m.SenderUserId == userB))))
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new SharedRequestInteraction
            {
                ParkingRequestId = r.Id,
                Address = r.Address,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();
    }

    public async Task AddAsync(UserReview review) => await _db.UserReviews.AddAsync(review);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
