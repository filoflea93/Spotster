namespace Spotster.Repositories;

public interface IParkingRequestBlockRepository
{
    Task<bool> IsBlockedAsync(Guid requestId, Guid guestUserId);
    Task<HashSet<Guid>> GetBlockedGuestIdsAsync(Guid requestId);
    Task<HashSet<Guid>> GetBlockedRequestIdsForGuestAsync(Guid guestUserId, IEnumerable<Guid> requestIds);
    Task AddAsync(Guid requestId, Guid guestUserId, Guid blockedByUserId);
    Task<bool> RemoveAsync(Guid requestId, Guid guestUserId);
    Task SaveChangesAsync();
}
