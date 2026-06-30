using Spotster.Data;
using Spotster.Entities;
using Microsoft.EntityFrameworkCore;

namespace Spotster.Repositories;

public class ParkingRequestBlockRepository : IParkingRequestBlockRepository
{
    private readonly AppDbContext _db;

    public ParkingRequestBlockRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> IsBlockedAsync(Guid requestId, Guid guestUserId) =>
        _db.Set<ParkingRequestBlock>().AnyAsync(b =>
            b.ParkingRequestId == requestId && b.GuestUserId == guestUserId);

    public async Task<HashSet<Guid>> GetBlockedGuestIdsAsync(Guid requestId)
    {
        var ids = await _db.Set<ParkingRequestBlock>()
            .Where(b => b.ParkingRequestId == requestId)
            .Select(b => b.GuestUserId)
            .ToListAsync();
        return ids.ToHashSet();
    }

    public async Task<HashSet<Guid>> GetBlockedRequestIdsForGuestAsync(
        Guid guestUserId,
        IEnumerable<Guid> requestIds)
    {
        var ids = requestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var blocked = await _db.Set<ParkingRequestBlock>()
            .Where(b => b.GuestUserId == guestUserId && ids.Contains(b.ParkingRequestId))
            .Select(b => b.ParkingRequestId)
            .ToListAsync();
        return blocked.ToHashSet();
    }

    public async Task AddAsync(Guid requestId, Guid guestUserId, Guid blockedByUserId)
    {
        if (await IsBlockedAsync(requestId, guestUserId))
        {
            return;
        }

        await _db.Set<ParkingRequestBlock>().AddAsync(new ParkingRequestBlock
        {
            Id = Guid.NewGuid(),
            ParkingRequestId = requestId,
            GuestUserId = guestUserId,
            BlockedByUserId = blockedByUserId,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<bool> RemoveAsync(Guid requestId, Guid guestUserId)
    {
        var block = await _db.Set<ParkingRequestBlock>()
            .FirstOrDefaultAsync(b =>
                b.ParkingRequestId == requestId && b.GuestUserId == guestUserId);
        if (block is null)
        {
            return false;
        }

        _db.Set<ParkingRequestBlock>().Remove(block);
        return true;
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
