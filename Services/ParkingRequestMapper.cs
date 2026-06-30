using System.Text.Json;
using Spotster.Domain;
using Spotster.DTOs;
using Spotster.Entities;

namespace Spotster.Services;

public static class ParkingRequestMapper
{
    public static ParkingRequestDto ToDto(
        ParkingRequest request,
        int messageCount = 0,
        int incomingMessageCount = 0,
        Func<string, string>? getPaymentLabel = null,
        bool canReserve = false,
        bool canUnreserve = false,
        bool isBlockedByOwner = false,
        bool viewerHasThread = false)
    {
        getPaymentLabel ??= PaymentMethodCodes.GetLabel;
        var methods = ParsePaymentMethods(request.PaymentMethodsJson);
        var now = DateTime.UtcNow;
        var canRenew = request.Status == ParkingRequestStatus.Active
            && request.ExpiresAt > now
            && request.RenewalCount < ListingConstants.MaxRequestRenewals;
        var isReserved = request.Status == ParkingRequestStatus.Reserved;

        return new ParkingRequestDto(
            request.Id,
            request.Latitude,
            request.Longitude,
            request.Address,
            request.RadiusMeters,
            request.CreatedAt,
            request.ExpiresAt,
            request.Status,
            request.CreatedByUser?.UserName ?? "unknown",
            request.CreatedByUserId,
            request.RewardAmount,
            request.Currency,
            methods,
            methods.Select(getPaymentLabel).ToList(),
            messageCount > 0 ? messageCount : request.Messages?.Count ?? 0,
            incomingMessageCount,
            request.RenewalCount,
            canRenew,
            isReserved,
            request.ReservedByUserId,
            request.ReservedByUser?.UserName,
            canReserve,
            canUnreserve,
            isBlockedByOwner,
            viewerHasThread);
    }

    public static ParkingRequestSignalDto ToSignalDto(ParkingRequest request) =>
        new(
            request.Id,
            request.Latitude,
            request.Longitude,
            request.RadiusMeters,
            request.Address,
            request.ExpiresAt);

    public static string SerializePaymentMethods(IReadOnlyList<string> methods) =>
        JsonSerializer.Serialize(methods);

    public static IReadOnlyList<string> ParsePaymentMethods(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return PaymentMethodCodes.Normalize(JsonSerializer.Deserialize<List<string>>(json));
        }
        catch
        {
            return [];
        }
    }
}
