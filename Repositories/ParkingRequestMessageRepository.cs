using Spotster.Data;
using Spotster.Entities;
using Microsoft.EntityFrameworkCore;

namespace Spotster.Repositories;

public class ParkingRequestMessageRepository : IParkingRequestMessageRepository
{
    private readonly AppDbContext _db;

    public ParkingRequestMessageRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<ParkingRequestMessage>> GetByRequestIdAsync(Guid requestId) =>
        _db.ParkingRequestMessages
            .AsNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => m.ParkingRequestId == requestId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

    public Task<List<ParkingRequestMessage>> GetByRequestAndGuestAsync(Guid requestId, Guid guestUserId) =>
        _db.ParkingRequestMessages
            .AsNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => m.ParkingRequestId == requestId && m.GuestUserId == guestUserId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

    public async Task<List<RequestThreadSummary>> GetThreadSummariesAsync(Guid requestId)
    {
        var messages = await _db.ParkingRequestMessages
            .AsNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => m.ParkingRequestId == requestId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        return messages
            .GroupBy(m => m.GuestUserId)
            .Select(g =>
            {
                var last = g.First();
                var guestMessage = g.FirstOrDefault(m => m.SenderUserId == g.Key);
                return new RequestThreadSummary
                {
                    GuestUserId = g.Key,
                    GuestUsername = guestMessage?.SenderUser?.UserName ?? last.SenderUser?.UserName ?? "user",
                    LastMessagePreview = last.Content.Length > 60 ? last.Content[..60] + "…" : last.Content,
                    LastMessageAt = last.CreatedAt,
                    MessageCount = g.Count(),
                    IncomingFromGuestCount = g.Count(m => m.SenderUserId == g.Key)
                };
            })
            .OrderByDescending(s => s.LastMessageAt)
            .ToList();
    }

    public Task<int> CountByRequestIdAsync(Guid requestId) =>
        _db.ParkingRequestMessages.CountAsync(m => m.ParkingRequestId == requestId);

    public async Task<Dictionary<Guid, int>> CountByRequestIdsAsync(IEnumerable<Guid> requestIds)
    {
        var ids = requestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _db.ParkingRequestMessages
            .Where(m => ids.Contains(m.ParkingRequestId))
            .GroupBy(m => m.ParkingRequestId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    public async Task<Dictionary<Guid, int>> CountIncomingByRequestIdsAsync(
        Guid ownerUserId,
        IEnumerable<Guid> requestIds)
    {
        var ids = requestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _db.ParkingRequestMessages
            .Where(m => ids.Contains(m.ParkingRequestId) && m.SenderUserId != ownerUserId)
            .GroupBy(m => m.ParkingRequestId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    public async Task<Dictionary<Guid, int>> CountIncomingForGuestOnRequestsAsync(
        Guid guestUserId,
        IEnumerable<Guid> requestIds)
    {
        var ids = requestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _db.ParkingRequestMessages
            .Where(m =>
                ids.Contains(m.ParkingRequestId) &&
                m.GuestUserId == guestUserId &&
                m.SenderUserId != guestUserId)
            .GroupBy(m => m.ParkingRequestId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    public async Task<Dictionary<Guid, int>> CountGuestSentOnRequestsAsync(
        Guid guestUserId,
        IEnumerable<Guid> requestIds)
    {
        var ids = requestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _db.ParkingRequestMessages
            .Where(m =>
                ids.Contains(m.ParkingRequestId) &&
                m.GuestUserId == guestUserId &&
                m.SenderUserId == guestUserId)
            .GroupBy(m => m.ParkingRequestId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    public async Task<Dictionary<Guid, int>> CountThreadMessagesForGuestOnRequestsAsync(
        Guid guestUserId,
        IEnumerable<Guid> requestIds)
    {
        var ids = requestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _db.ParkingRequestMessages
            .Where(m => ids.Contains(m.ParkingRequestId) && m.GuestUserId == guestUserId)
            .GroupBy(m => m.ParkingRequestId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    public async Task AddAsync(ParkingRequestMessage message) =>
        await _db.ParkingRequestMessages.AddAsync(message);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
