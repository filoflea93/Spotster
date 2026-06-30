using Spotster.Entities;

namespace Spotster.Repositories;

public interface IParkingRepository
{
    Task<ParkingReport?> GetByIdAsync(Guid id);
    Task<List<ParkingReport>> GetActiveAsync();
    Task<List<ParkingReport>> GetActiveByUserAsync(Guid userId);
    Task<List<ParkingReport>> GetNearbyAsync(double latitude, double longitude, double radiusMeters, int page, int pageSize);
    Task<int> CountNearbyAsync(double latitude, double longitude, double radiusMeters);
    Task<List<ParkingReport>> GetReadyToPurgeAsync(DateTime utcNow);
    Task<int> CountRecentReportsByUserAsync(Guid userId, DateTime since);
    Task<int> CountActiveReportsByUserAsync(Guid userId);
    Task<int> CountRecentReportsAtZoneAsync(Guid userId, string zoneKey, DateTime since);
    Task<ParkingReport?> FindActiveInZoneAsync(string zoneKey);
    Task<int> CountReportsTodayAsync();
    Task<int> CountActiveAsync();
    Task AddAsync(ParkingReport report);
    Task AddVoteAsync(ReportVote vote);
    void Delete(ParkingReport report);
    Task SaveChangesAsync();
}
