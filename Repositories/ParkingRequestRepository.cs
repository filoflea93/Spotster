using Spotster.Data;
using Spotster.Domain.Geo;
using Spotster.Entities;
using Spotster.Infrastructure.Geo;
using Microsoft.EntityFrameworkCore;

namespace Spotster.Repositories;

public class ParkingRequestRepository : IParkingRequestRepository
{
    private static readonly ParkingRequestStatus[] LiveStatuses =
        [ParkingRequestStatus.Active, ParkingRequestStatus.Reserved];

    private readonly AppDbContext _db;

    public ParkingRequestRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ParkingRequest?> GetByIdAsync(Guid id) =>
        _db.ParkingRequests
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReservedByUser)
            .FirstOrDefaultAsync(r => r.Id == id);

    public Task<List<ParkingRequest>> GetActiveAsync() =>
        _db.ParkingRequests
            .AsNoTracking()
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReservedByUser)
            .Where(r => LiveStatuses.Contains(r.Status) && r.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public async Task<List<ParkingRequest>> GetNearbyAsync(double latitude, double longitude, double searchRadiusMeters)
    {
        var utcNow = DateTime.UtcNow;
        var searchPoint = GeoPointFactory.Create(latitude, longitude);
        searchRadiusMeters = Math.Clamp(searchRadiusMeters, 50, GeoConstants.NearbyMaxRadiusMeters);

        var candidates = await _db.ParkingRequests
            .AsNoTracking()
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReservedByUser)
            .Where(r => LiveStatuses.Contains(r.Status) && r.ExpiresAt > utcNow)
            .Where(r => r.Location.Distance(searchPoint) <=
                (r.RadiusMeters > searchRadiusMeters ? r.RadiusMeters : searchRadiusMeters))
            .OrderBy(r => r.Location.Distance(searchPoint))
            .ToListAsync();

        return candidates;
    }

    public Task<List<ParkingRequest>> GetReadyToPurgeAsync(DateTime utcNow) =>
        _db.ParkingRequests
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReservedByUser)
            .Where(r => r.ExpiresAt <= utcNow)
            .ToListAsync();

    public Task<int> CountRecentByUserAsync(Guid userId, DateTime since) =>
        _db.ParkingRequests.CountAsync(r => r.CreatedByUserId == userId && r.CreatedAt >= since);

    public Task<int> CountActiveByUserAsync(Guid userId)
    {
        var utcNow = DateTime.UtcNow;
        return _db.ParkingRequests.CountAsync(r =>
            r.CreatedByUserId == userId &&
            LiveStatuses.Contains(r.Status) &&
            r.ExpiresAt > utcNow);
    }

    public Task<int> CountCreatedTodayByUserAsync(Guid userId, DateTime dayStartUtc) =>
        _db.ParkingRequests.CountAsync(r =>
            r.CreatedByUserId == userId &&
            r.CreatedAt >= dayStartUtc);

    public Task<List<ParkingRequest>> GetActiveByUserAsync(Guid userId) =>
        _db.ParkingRequests
            .AsNoTracking()
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReservedByUser)
            .Where(r => r.CreatedByUserId == userId &&
                        LiveStatuses.Contains(r.Status) &&
                        r.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public async Task AddAsync(ParkingRequest request) => await _db.ParkingRequests.AddAsync(request);

    public void Delete(ParkingRequest request) => _db.ParkingRequests.Remove(request);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
