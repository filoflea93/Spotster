namespace Spotster.DTOs;

using Spotster.Entities;

public record GeocodeResultDto(double Latitude, double Longitude, string FormattedAddress);

public record AddressSuggestionDto(
    double Latitude,
    double Longitude,
    string FormattedAddress,
    string ShortLabel);

public record CreateParkingSearchRequest(
    string Address,
    int RadiusMeters,
    decimal? RewardAmount,
    IReadOnlyList<string>? PaymentMethods);

public record CreateParkingSearchByCoordsRequest(
    double Latitude,
    double Longitude,
    string? Address,
    int RadiusMeters,
    decimal? RewardAmount,
    IReadOnlyList<string>? PaymentMethods);

public record ParkingRequestDto(
    Guid Id,
    double Latitude,
    double Longitude,
    string Address,
    int RadiusMeters,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    ParkingRequestStatus Status,
    string CreatedByUsername,
    Guid CreatedByUserId,
    decimal? RewardAmount,
    string Currency,
    IReadOnlyList<string> PaymentMethods,
    IReadOnlyList<string> PaymentMethodLabels,
    int MessageCount,
    int IncomingMessageCount,
    int RenewalCount,
    bool CanRenew,
    bool IsReserved,
    Guid? ReservedByUserId,
    string? ReservedByUsername,
    bool CanReserve,
    bool CanUnreserve,
    bool IsBlockedByOwner,
    bool ViewerHasThread);

public record ReserveParkingRequestDto(Guid? GuestUserId = null);

public record BlockParkingGuestRequestDto(Guid GuestUserId);

public record ParkingRequestSignalDto(
    Guid Id,
    double Latitude,
    double Longitude,
    int RadiusMeters,
    string Address,
    DateTime ExpiresAt);
