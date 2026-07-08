using Spotster.Data;
using Spotster.Domain;
using Spotster.Domain.Geo;
using Spotster.Entities;
using Spotster.Infrastructure.Geo;
using Microsoft.EntityFrameworkCore;

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
            .AsSplitQuery()
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
            .FirstOrDefaultAsync(p => p.Id == id);

    public Task<List<ParkingReport>> GetActiveAsync() =>
        ActiveReportsQuery(DateTime.UtcNow)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
            .OrderByDescending(p => p.LastUpdatedAt)
            .Take(ListingConstants.MaxActiveListSize)
            .ToListAsync();

    public Task<List<ParkingReport>> GetActiveByUserAsync(Guid userId) =>
        ActiveReportsQuery(DateTime.UtcNow)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
            .Where(p => p.CreatedByUserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<List<ParkingReport>> GetNearbyAsync(double latitude, double longitude, double radiusMeters, int page, int pageSize)
    {
        var utcNow = DateTime.UtcNow;
        var searchPoint = GeoPointFactory.Create(latitude, longitude);

        return await ActiveReportsQuery(utcNow)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
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

        return ActiveReportsQuery(utcNow)
            .AsNoTracking()
            .Where(p => p.Location.Distance(searchPoint) <= radiusMeters)
            .CountAsync();
    }

    public Task<List<ParkingReport>> GetReadyToPurgeAsync(DateTime utcNow) =>
        _db.ParkingReports
            .AsSplitQuery()
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
        ActiveReportsQuery(DateTime.UtcNow)
            .AsSplitQuery()
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
            .Where(p => p.VirtualZoneKey == zoneKey)
            .OrderByDescending(p => p.LastUpdatedAt)
            .FirstOrDefaultAsync();

    public Task<ParkingReport?> FindNearestActiveAsync(double latitude, double longitude, double radiusMeters)
    {
        var utcNow = DateTime.UtcNow;
        var searchPoint = GeoPointFactory.Create(latitude, longitude);

        return ActiveReportsQuery(utcNow)
            .AsSplitQuery()
            .Include(p => p.CreatedByUser)
            .Include(p => p.Votes)
            .Where(p => p.Location.Distance(searchPoint) <= radiusMeters)
            .OrderBy(p => p.Location.Distance(searchPoint))
            .FirstOrDefaultAsync();
    }

    private IQueryable<ParkingReport> ActiveReportsQuery(DateTime utcNow) =>
        _db.ParkingReports.Where(p => p.Status == ParkingStatus.Active && p.ExpiresAt > utcNow);

    public Task<int> CountReportsTodayAsync()
    {
        var today = DateTime.UtcNow.Date;
        return _db.ParkingReports.CountAsync(p => p.CreatedAt >= today);
    }

    public Task<int> CountActiveAsync() =>
        _db.ParkingReports.CountAsync(p => p.Status == ParkingStatus.Active && p.ExpiresAt > DateTime.UtcNow);

    public Task<int> CountValidThumbsUpReceivedAsync(Guid userId) =>
        _db.ReportVotes.CountAsync(v =>
            v.IsValid &&
            v.ParkingReport.CreatedByUserId == userId);

    public async Task AddAsync(ParkingReport report) => await _db.ParkingReports.AddAsync(report);

    public async Task AddVoteAsync(ReportVote vote) => await _db.ReportVotes.AddAsync(vote);

    public void Delete(ParkingReport report) => _db.ParkingReports.Remove(report);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
