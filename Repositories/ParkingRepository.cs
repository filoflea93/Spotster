using Spotster.Data;
using Spotster.Domain.Geo;
using Spotster.Entities;
using Spotster.Infrastructure.Geo;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Spotster.Repositories;

public class ParkingRepository : IParkingRepository
{
    private readonly AppDbContext _db;

    public ParkingRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ParkingReport?> GetByIdAsync(Guid id) =>
        _db.ParkingReports
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
            .FirstOrDefaultAsync(p => p.Id == id);

    public Task<List<ParkingReport>> GetActiveAsync() =>
        _db.ParkingReports
            .AsNoTracking()
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
            .Where(p => p.Status == ParkingStatus.Active && p.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(p => p.LastUpdatedAt)
            .ToListAsync();

    public Task<List<ParkingReport>> GetActiveByUserAsync(Guid userId) =>
        _db.ParkingReports
            .AsNoTracking()
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
            .Where(p => p.CreatedByUserId == userId &&
                        p.Status == ParkingStatus.Active &&
                        p.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<List<ParkingReport>> GetNearbyAsync(double latitude, double longitude, double radiusMeters, int page, int pageSize)
    {
        var utcNow = DateTime.UtcNow;
        var searchPoint = GeoPointFactory.Create(latitude, longitude);

        return await _db.ParkingReports
            .AsNoTracking()
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
            .Where(p => p.Status == ParkingStatus.Active && p.ExpiresAt > utcNow)
            .Where(p => p.Location.Distance(searchPoint) <= radiusMeters)
            .OrderByDescending(p => p.ConfidenceScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> CountNearbyAsync(double latitude, double longitude, double radiusMeters)
    {
        var utcNow = DateTime.UtcNow;
        var searchPoint = GeoPointFactory.Create(latitude, longitude);

        return _db.ParkingReports
            .AsNoTracking()
            .Where(p => p.Status == ParkingStatus.Active && p.ExpiresAt > utcNow)
            .Where(p => p.Location.Distance(searchPoint) <= radiusMeters)
            .CountAsync();
    }

    public Task<List<ParkingReport>> GetReadyToPurgeAsync(DateTime utcNow) =>
        _db.ParkingReports
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
            .Where(p => p.ExpiresAt <= utcNow)
            .ToListAsync();

    public Task<int> CountRecentReportsByUserAsync(Guid userId, DateTime since) =>
        _db.ParkingReports.CountAsync(p => p.CreatedByUserId == userId && p.CreatedAt >= since);

    public Task<int> CountActiveReportsByUserAsync(Guid userId)
    {
        var utcNow = DateTime.UtcNow;
        return _db.ParkingReports.CountAsync(p =>
            p.CreatedByUserId == userId &&
            p.Status == ParkingStatus.Active &&
            p.ExpiresAt > utcNow);
    }

    public Task<int> CountRecentReportsAtZoneAsync(Guid userId, string zoneKey, DateTime since) =>
        _db.ParkingReports.CountAsync(p =>
            p.CreatedByUserId == userId &&
            p.VirtualZoneKey == zoneKey &&
            p.CreatedAt >= since);

    public Task<ParkingReport?> FindActiveInZoneAsync(string zoneKey) =>
        _db.ParkingReports
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
            .Where(p => p.Status == ParkingStatus.Active && p.ExpiresAt > DateTime.UtcNow && p.VirtualZoneKey == zoneKey)
            .OrderByDescending(p => p.LastUpdatedAt)
            .FirstOrDefaultAsync();

    public Task<int> CountReportsTodayAsync()
    {
        var today = DateTime.UtcNow.Date;
        return _db.ParkingReports.CountAsync(p => p.CreatedAt >= today);
    }

    public Task<int> CountActiveAsync() =>
        _db.ParkingReports.CountAsync(p => p.Status == ParkingStatus.Active && p.ExpiresAt > DateTime.UtcNow);

    public async Task AddAsync(ParkingReport report) => await _db.ParkingReports.AddAsync(report);

    public async Task AddVoteAsync(ReportVote vote) => await _db.ReportVotes.AddAsync(vote);

    public void Delete(ParkingReport report) => _db.ParkingReports.Remove(report);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
