using Spotster.Entities;

namespace Spotster.Repositories;

public interface IParkingRequestRepository
{
    Task<ParkingRequest?> GetByIdAsync(Guid id);
    Task<List<ParkingRequest>> GetActiveAsync();
    Task<List<ParkingRequest>> GetNearbyAsync(double latitude, double longitude, double searchRadiusMeters);
    Task<List<ParkingRequest>> GetReadyToPurgeAsync(DateTime utcNow);
    Task<int> CountRecentByUserAsync(Guid userId, DateTime since);
    Task<int> CountActiveByUserAsync(Guid userId);
    Task<int> CountCreatedTodayByUserAsync(Guid userId, DateTime dayStartUtc);
    Task<List<ParkingRequest>> GetActiveByUserAsync(Guid userId);
    Task AddAsync(ParkingRequest request);
    void Delete(ParkingRequest request);
    Task SaveChangesAsync();
}
