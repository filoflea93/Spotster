using Spotster.DTOs;

namespace Spotster.Services;

public interface IParkingRequestService
{
    Task<GeocodeResultDto> GeocodeAsync(string address);
    Task<IReadOnlyList<AddressSuggestionDto>> SuggestAddressesAsync(
        string query,
        double? biasLatitude,
        double? biasLongitude);
    Task<ParkingRequestDto> CreateRequestAsync(
        Guid userId,
        CreateParkingSearchRequest request,
        string? ipAddress,
        string? userAgent);
    Task<IReadOnlyList<ParkingRequestDto>> GetNearbyAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        Guid? viewerUserId = null);
    Task<IReadOnlyList<ParkingRequestDto>> GetMyActiveAsync(Guid userId);
    Task<ParkingRequestDto> RenewRequestAsync(Guid userId, Guid requestId);
    Task<ParkingRequestDto> ReserveRequestAsync(Guid userId, Guid requestId, Guid? guestUserId = null);
    Task<ParkingRequestDto> UnreserveRequestAsync(Guid userId, Guid requestId);
    Task BlockGuestAsync(Guid userId, Guid requestId, Guid guestUserId);
    Task UnblockGuestAsync(Guid userId, Guid requestId, Guid guestUserId);
    Task<ParkingRequestDto> UpdateRequestAsync(Guid userId, Guid requestId, CreateParkingSearchRequest request);
    Task DeleteRequestAsync(Guid userId, Guid requestId);
    Task ProcessExpiredRequestsAsync();
    Task NotifyNearbyRequestsOnReportAsync(double latitude, double longitude, Guid reporterUserId);
}
