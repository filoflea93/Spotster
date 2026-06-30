using Spotster.Entities;

namespace Spotster.Repositories;

public class RequestThreadSummary
{
    public Guid GuestUserId { get; set; }
    public string GuestUsername { get; set; } = string.Empty;
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int MessageCount { get; set; }
    public int IncomingFromGuestCount { get; set; }
}

public interface IParkingRequestMessageRepository
{
    Task<List<ParkingRequestMessage>> GetByRequestIdAsync(Guid requestId);
    Task<List<ParkingRequestMessage>> GetByRequestAndGuestAsync(Guid requestId, Guid guestUserId);
    Task<List<RequestThreadSummary>> GetThreadSummariesAsync(Guid requestId);
    Task<int> CountByRequestIdAsync(Guid requestId);
    Task<Dictionary<Guid, int>> CountByRequestIdsAsync(IEnumerable<Guid> requestIds);
    Task<Dictionary<Guid, int>> CountIncomingByRequestIdsAsync(Guid ownerUserId, IEnumerable<Guid> requestIds);
    Task<Dictionary<Guid, int>> CountIncomingForGuestOnRequestsAsync(Guid guestUserId, IEnumerable<Guid> requestIds);
    Task<Dictionary<Guid, int>> CountGuestSentOnRequestsAsync(Guid guestUserId, IEnumerable<Guid> requestIds);
    Task<Dictionary<Guid, int>> CountThreadMessagesForGuestOnRequestsAsync(Guid guestUserId, IEnumerable<Guid> requestIds);
    Task AddAsync(ParkingRequestMessage message);
    Task SaveChangesAsync();
}
