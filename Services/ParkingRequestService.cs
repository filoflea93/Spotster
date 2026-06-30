using Spotster.Domain;
using Spotster.Domain.Geo;
using Spotster.DTOs;
using Spotster.Entities;
using Spotster.Infrastructure.Geo;
using Spotster.Repositories;
using Spotster.Resources;
using Spotster.Services.AntiFraud;
using Spotster.Services.Cache;
using Spotster.Services.Geo;
using Spotster.Services.Localization;
using Spotster.Services.Realtime;
using Microsoft.Extensions.Localization;

namespace Spotster.Services;

public class ParkingRequestService : IParkingRequestService
{
    private const int MinRadiusMeters = 100;
    private const int MaxRadiusMeters = 5000;
    private const decimal MaxRewardAmount = 100m;

    private readonly IParkingRequestRepository _requests;
    private readonly IParkingRequestMessageRepository _messages;
    private readonly IParkingRequestBlockRepository _blocks;
    private readonly IUserRepository _users;
    private readonly IGeocodingService _geocoding;
    private readonly IAntiFraudService _antiFraud;
    private readonly IParkingRequestCacheService _requestCache;
    private readonly IParkingRealtimeNotifier _notifier;
    private readonly ILogger<ParkingRequestService> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly PaymentMethodLabels _paymentLabels;

    public ParkingRequestService(
        IParkingRequestRepository requests,
        IParkingRequestMessageRepository messages,
        IParkingRequestBlockRepository blocks,
        IUserRepository users,
        IGeocodingService geocoding,
        IAntiFraudService antiFraud,
        IParkingRequestCacheService requestCache,
        IParkingRealtimeNotifier notifier,
        ILogger<ParkingRequestService> logger,
        IStringLocalizer<SharedResources> localizer,
        PaymentMethodLabels paymentLabels)
    {
        _requests = requests;
        _messages = messages;
        _blocks = blocks;
        _users = users;
        _geocoding = geocoding;
        _antiFraud = antiFraud;
        _requestCache = requestCache;
        _notifier = notifier;
        _logger = logger;
        _localizer = localizer;
        _paymentLabels = paymentLabels;
    }

    public Task<GeocodeResultDto> GeocodeAsync(string address) =>
        _geocoding.GeocodeAddressAsync(address);

    public Task<IReadOnlyList<AddressSuggestionDto>> SuggestAddressesAsync(
        string query,
        double? biasLatitude,
        double? biasLongitude) =>
        _geocoding.SuggestAddressesAsync(query, biasLatitude, biasLongitude);

    public async Task<ParkingRequestDto> CreateRequestAsync(
        Guid userId,
        CreateParkingSearchRequest request,
        string? ipAddress,
        string? userAgent)
    {
        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorized"]);
        }

        var (radiusMeters, rewardAmount, paymentMethods) = ParseRequestFields(request);

        var activeCount = await _requests.CountActiveByUserAsync(userId);
        if (activeCount >= ListingConstants.MaxActiveRequestsPerUser)
        {
            throw new InvalidOperationException(_localizer["Error_MaxActiveRequests"]);
        }

        var todayStart = DateTime.UtcNow.Date;
        var todayCount = await _requests.CountCreatedTodayByUserAsync(userId, todayStart);
        if (todayCount >= ListingConstants.MaxRequestsPerDay)
        {
            throw new InvalidOperationException(_localizer["Error_DailyRequestLimit"]);
        }

        var geocoded = await _geocoding.GeocodeAddressAsync(request.Address);
        var lat = geocoded.Latitude;
        var lng = geocoded.Longitude;
        var resolvedAddress = geocoded.FormattedAddress;

        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorized"]);
        }

        var user = await _users.GetByIdAsync(userId)
            ?? throw new UnauthorizedAccessException(_localizer["Error_UserNotFound"]);

        user.LastActivityAt = DateTime.UtcNow;
        await _users.SaveChangesAsync();

        var snapped = GeoHelper.SnapToVirtualZone(lat, lng);
        var now = DateTime.UtcNow;

        var parkingRequest = new ParkingRequest
        {
            Id = Guid.NewGuid(),
            Address = resolvedAddress,
            RadiusMeters = radiusMeters,
            VirtualZoneKey = GeoHelper.GetVirtualZoneKey(lat, lng),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(ListingConstants.TtlMinutes),
            Status = ParkingRequestStatus.Active,
            RenewalCount = 0,
            RewardAmount = rewardAmount,
            Currency = "EUR",
            PaymentMethodsJson = ParkingRequestMapper.SerializePaymentMethods(paymentMethods),
            CreatedByUserId = userId
        };
        GeoPointFactory.ApplyCoordinates(parkingRequest, snapped.Latitude, snapped.Longitude);

        await _requests.AddAsync(parkingRequest);
        parkingRequest.CreatedByUser = user;
        await _requests.SaveChangesAsync();
        await _requestCache.InvalidateAsync();

        var dto = ParkingRequestMapper.ToDto(parkingRequest, getPaymentLabel: _paymentLabels.Get);
        await _notifier.BroadcastMapEventAsync(
            parkingRequest.Latitude,
            parkingRequest.Longitude,
            "ParkingRequestCreated",
            ParkingRequestMapper.ToSignalDto(parkingRequest));
        _logger.LogInformation("Parking request {Id} in area {Address}", parkingRequest.Id, resolvedAddress);
        return dto;
    }

    public async Task<IReadOnlyList<ParkingRequestDto>> GetNearbyAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        Guid? viewerUserId = null)
    {
        var items = await _requests.GetNearbyAsync(latitude, longitude, radiusMeters);
        var ids = items.Select(i => i.Id).ToList();
        var counts = await _messages.CountByRequestIdsAsync(ids);

        Dictionary<Guid, int> incomingCounts = new();
        Dictionary<Guid, int> guestSentCounts = new();
        Dictionary<Guid, int> guestThreadCounts = new();
        HashSet<Guid> blockedRequestIds = new();
        if (viewerUserId.HasValue && viewerUserId.Value != Guid.Empty)
        {
            var guestIds = items
                .Where(i => i.CreatedByUserId != viewerUserId.Value)
                .Select(i => i.Id)
                .ToList();
            if (guestIds.Count > 0)
            {
                incomingCounts = await _messages.CountIncomingForGuestOnRequestsAsync(
                    viewerUserId.Value,
                    guestIds);
                guestSentCounts = await _messages.CountGuestSentOnRequestsAsync(
                    viewerUserId.Value,
                    guestIds);
                guestThreadCounts = await _messages.CountThreadMessagesForGuestOnRequestsAsync(
                    viewerUserId.Value,
                    guestIds);
                blockedRequestIds = await _blocks.GetBlockedRequestIdsForGuestAsync(
                    viewerUserId.Value,
                    guestIds);
            }
        }

        return items
            .Select(i => MapToDto(
                i,
                viewerUserId,
                counts.GetValueOrDefault(i.Id),
                incomingCounts.GetValueOrDefault(i.Id),
                guestSentCounts.GetValueOrDefault(i.Id),
                guestThreadCounts.GetValueOrDefault(i.Id),
                blockedRequestIds.Contains(i.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<ParkingRequestDto>> GetMyActiveAsync(Guid userId)
    {
        var items = await _requests.GetActiveByUserAsync(userId);
        var ids = items.Select(i => i.Id).ToList();
        var counts = await _messages.CountByRequestIdsAsync(ids);
        var incomingCounts = await _messages.CountIncomingByRequestIdsAsync(userId, ids);
        return items
            .Select(i => MapToDto(
                i,
                userId,
                counts.GetValueOrDefault(i.Id),
                incomingCounts.GetValueOrDefault(i.Id)))
            .ToList();
    }

    public async Task<ParkingRequestDto> ReserveRequestAsync(
        Guid userId,
        Guid requestId,
        Guid? guestUserId = null)
    {
        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorized"]);
        }

        var request = await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException(_localizer["Error_RequestNotFound"]);

        if (request.CreatedByUserId != userId)
        {
            throw new UnauthorizedAccessException(_localizer["Error_ReserveOwnerOnly"]);
        }

        if (!guestUserId.HasValue || guestUserId.Value == userId)
        {
            throw new ArgumentException(_localizer["Error_ReserveGuestRequired"]);
        }

        var negotiatingGuestId = guestUserId.Value;

        if (await _blocks.IsBlockedAsync(requestId, negotiatingGuestId))
        {
            throw new InvalidOperationException(_localizer["Error_UserBlockedOnRequest"]);
        }

        var now = DateTime.UtcNow;
        if (request.ExpiresAt <= now)
        {
            throw new InvalidOperationException(_localizer["Error_RequestInactive"]);
        }

        if (request.Status == ParkingRequestStatus.Reserved)
        {
            if (request.ReservedByUserId == negotiatingGuestId)
            {
                return await BuildDtoForViewerAsync(request, userId);
            }

            throw new InvalidOperationException(_localizer["Error_RequestReserved"]);
        }

        if (request.Status != ParkingRequestStatus.Active)
        {
            throw new InvalidOperationException(_localizer["Error_RequestInactive"]);
        }

        var threadMessages = await _messages.GetByRequestAndGuestAsync(requestId, negotiatingGuestId);
        if (!threadMessages.Any(m => m.SenderUserId == negotiatingGuestId))
        {
            throw new InvalidOperationException(_localizer["Error_ReserveNeedsMessage"]);
        }

        var guestUser = await _users.GetByIdAsync(negotiatingGuestId)
            ?? throw new KeyNotFoundException(_localizer["Error_UserNotFound"]);

        request.Status = ParkingRequestStatus.Reserved;
        request.ReservedByUserId = negotiatingGuestId;
        request.ReservedAt = now;
        request.ExpiresAt = now.AddMinutes(ListingConstants.ReservationExtensionMinutes);
        request.ReservedByUser = guestUser;

        await _requests.SaveChangesAsync();
        await _requestCache.InvalidateAsync();

        var dto = await BuildDtoForViewerAsync(request, userId);
        await _notifier.BroadcastMapEventAsync(
            request.Latitude, request.Longitude, "ParkingRequestReserved", ParkingRequestMapper.ToSignalDto(request));
        _logger.LogInformation(
            "Parking request {Id} marked as in negotiation with guest {GuestId} by {UserId}",
            request.Id,
            negotiatingGuestId,
            userId);
        return dto;
    }

    public async Task<ParkingRequestDto> UnreserveRequestAsync(Guid userId, Guid requestId)
    {
        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorized"]);
        }

        var request = await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException(_localizer["Error_RequestNotFound"]);

        if (request.CreatedByUserId != userId)
        {
            throw new UnauthorizedAccessException(_localizer["Error_UnreserveOwnerOnly"]);
        }

        if (request.Status != ParkingRequestStatus.Reserved || request.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException(_localizer["Error_RequestNotReserved"]);
        }

        request.Status = ParkingRequestStatus.Active;
        request.ReservedByUserId = null;
        request.ReservedAt = null;
        request.ReservedByUser = null;

        await _requests.SaveChangesAsync();
        await _requestCache.InvalidateAsync();

        var dto = await BuildDtoForViewerAsync(request, userId);
        await _notifier.BroadcastMapEventAsync(
            request.Latitude, request.Longitude, "ParkingRequestUnreserved", ParkingRequestMapper.ToSignalDto(request));
        _logger.LogInformation("Parking request {Id} removed from negotiation by {UserId}", request.Id, userId);
        return dto;
    }

    public async Task BlockGuestAsync(Guid userId, Guid requestId, Guid guestUserId)
    {
        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorized"]);
        }

        var request = await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException(_localizer["Error_RequestNotFound"]);

        if (request.CreatedByUserId != userId)
        {
            throw new UnauthorizedAccessException(_localizer["Error_BlockGuestOwnerOnly"]);
        }

        if (guestUserId == userId)
        {
            throw new ArgumentException(_localizer["Error_BlockGuestInvalid"]);
        }

        if (await _blocks.IsBlockedAsync(requestId, guestUserId))
        {
            return;
        }

        var threadMessages = await _messages.GetByRequestAndGuestAsync(requestId, guestUserId);
        if (threadMessages.Count == 0)
        {
            throw new InvalidOperationException(_localizer["Error_BlockGuestNeedsThread"]);
        }

        await _blocks.AddAsync(requestId, guestUserId, userId);

        if (request.Status == ParkingRequestStatus.Reserved && request.ReservedByUserId == guestUserId)
        {
            request.Status = ParkingRequestStatus.Active;
            request.ReservedByUserId = null;
            request.ReservedAt = null;
            request.ReservedByUser = null;
        }

        await _blocks.SaveChangesAsync();
        await _requests.SaveChangesAsync();
        await _requestCache.InvalidateAsync();

        var blockedPayload = new
        {
            requestId,
            guestUserId,
            ownerUserId = userId
        };
        await _notifier.NotifyRequestAsync(requestId, "ParkingRequestGuestBlocked", blockedPayload);
        _logger.LogInformation(
            "User {GuestId} blocked on request {RequestId} by {OwnerId}",
            guestUserId,
            requestId,
            userId);
    }

    public async Task UnblockGuestAsync(Guid userId, Guid requestId, Guid guestUserId)
    {
        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorized"]);
        }

        var request = await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException(_localizer["Error_RequestNotFound"]);

        if (request.CreatedByUserId != userId)
        {
            throw new UnauthorizedAccessException(_localizer["Error_BlockGuestOwnerOnly"]);
        }

        if (guestUserId == userId)
        {
            throw new ArgumentException(_localizer["Error_BlockGuestInvalid"]);
        }

        if (!await _blocks.RemoveAsync(requestId, guestUserId))
        {
            throw new InvalidOperationException(_localizer["Error_UnblockGuestNotBlocked"]);
        }

        await _blocks.SaveChangesAsync();
        await _requestCache.InvalidateAsync();

        var unblockedPayload = new
        {
            requestId,
            guestUserId,
            ownerUserId = userId
        };
        await _notifier.NotifyRequestAsync(requestId, "ParkingRequestGuestUnblocked", unblockedPayload);
        _logger.LogInformation(
            "User {GuestId} unblocked on request {RequestId} by {OwnerId}",
            guestUserId,
            requestId,
            userId);
    }

    public async Task<ParkingRequestDto> RenewRequestAsync(Guid userId, Guid requestId)
    {
        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorized"]);
        }

        var request = await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException(_localizer["Error_RequestNotFound"]);

        if (request.CreatedByUserId != userId)
        {
            throw new UnauthorizedAccessException(_localizer["Error_RenewOwnOnly"]);
        }

        if (request.Status != ParkingRequestStatus.Active || request.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException(_localizer["Error_RequestInactive"]);
        }

        if (request.RenewalCount >= ListingConstants.MaxRequestRenewals)
        {
            throw new InvalidOperationException(_localizer["Error_RenewUsed"]);
        }

        var now = DateTime.UtcNow;
        request.RenewalCount++;
        request.ExpiresAt = now.AddMinutes(ListingConstants.TtlMinutes);
        await _requests.SaveChangesAsync();
        await _requestCache.InvalidateAsync();

        var ids = new[] { request.Id };
        var counts = await _messages.CountByRequestIdsAsync(ids);
        var incomingCounts = await _messages.CountIncomingByRequestIdsAsync(userId, ids);
        var dto = ParkingRequestMapper.ToDto(
            request,
            counts.GetValueOrDefault(request.Id),
            incomingCounts.GetValueOrDefault(request.Id),
            _paymentLabels.Get);

        await _notifier.BroadcastMapEventAsync(
            request.Latitude, request.Longitude, "ParkingRequestRenewed", ParkingRequestMapper.ToSignalDto(request));
        _logger.LogInformation("Parking request {Id} renewed by {UserId}", request.Id, userId);
        return dto;
    }

    public async Task<ParkingRequestDto> UpdateRequestAsync(
        Guid userId,
        Guid requestId,
        CreateParkingSearchRequest request)
    {
        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorized"]);
        }

        var parkingRequest = await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException(_localizer["Error_RequestNotFound"]);

        if (parkingRequest.CreatedByUserId != userId)
        {
            throw new UnauthorizedAccessException(_localizer["Error_UpdateRequestOwnOnly"]);
        }

        if (!IsLiveListing(parkingRequest) || parkingRequest.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException(_localizer["Error_RequestInactive"]);
        }

        var (radiusMeters, rewardAmount, paymentMethods) = ParseRequestFields(request);

        var geocoded = await _geocoding.GeocodeAddressAsync(request.Address);
        var lat = geocoded.Latitude;
        var lng = geocoded.Longitude;
        var snapped = GeoHelper.SnapToVirtualZone(lat, lng);

        GeoPointFactory.ApplyCoordinates(parkingRequest, snapped.Latitude, snapped.Longitude);
        parkingRequest.Address = geocoded.FormattedAddress;
        parkingRequest.RadiusMeters = radiusMeters;
        parkingRequest.VirtualZoneKey = GeoHelper.GetVirtualZoneKey(lat, lng);
        parkingRequest.RewardAmount = rewardAmount;
        parkingRequest.PaymentMethodsJson = ParkingRequestMapper.SerializePaymentMethods(paymentMethods);

        await _requests.SaveChangesAsync();
        await _requestCache.InvalidateAsync();

        var ids = new[] { parkingRequest.Id };
        var counts = await _messages.CountByRequestIdsAsync(ids);
        var incomingCounts = await _messages.CountIncomingByRequestIdsAsync(userId, ids);
        var dto = ParkingRequestMapper.ToDto(
            parkingRequest,
            counts.GetValueOrDefault(parkingRequest.Id),
            incomingCounts.GetValueOrDefault(parkingRequest.Id),
            _paymentLabels.Get);

        await _notifier.BroadcastMapEventAsync(
            parkingRequest.Latitude,
            parkingRequest.Longitude,
            "ParkingRequestUpdated",
            ParkingRequestMapper.ToSignalDto(parkingRequest));
        _logger.LogInformation("Parking request {Id} updated by {UserId}", parkingRequest.Id, userId);
        return dto;
    }

    public async Task DeleteRequestAsync(Guid userId, Guid requestId)
    {
        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorized"]);
        }

        var request = await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException(_localizer["Error_RequestNotFound"]);

        if (request.CreatedByUserId != userId)
        {
            throw new UnauthorizedAccessException(_localizer["Error_DeleteRequestOwnOnly"]);
        }

        await _notifier.BroadcastMapEventAsync(
            request.Latitude, request.Longitude, "ParkingRequestExpired", ParkingRequestMapper.ToSignalDto(request));
        _requests.Delete(request);
        await _requests.SaveChangesAsync();
        await _requestCache.InvalidateAsync();
        _logger.LogInformation("Parking request {Id} deleted by {UserId}", requestId, userId);
    }

    public async Task ProcessExpiredRequestsAsync()
    {
        var toPurge = await _requests.GetReadyToPurgeAsync(DateTime.UtcNow);
        foreach (var request in toPurge)
        {
            await _notifier.BroadcastMapEventAsync(
                request.Latitude, request.Longitude, "ParkingRequestExpired", ParkingRequestMapper.ToSignalDto(request));
            _requests.Delete(request);
            _logger.LogInformation("Parking request {Id} deleted after expiration", request.Id);
        }

        if (toPurge.Count > 0)
        {
            await _requests.SaveChangesAsync();
            await _requestCache.InvalidateAsync();
        }
    }

    public async Task NotifyNearbyRequestsOnReportAsync(
        double latitude,
        double longitude,
        Guid reporterUserId)
    {
        var active = await _requests.GetNearbyAsync(latitude, longitude, 5000);

        foreach (var request in active)
        {
            if (request.CreatedByUserId == reporterUserId)
            {
                continue;
            }

            var distance = GeoHelper.DistanceMeters(latitude, longitude, request.Latitude, request.Longitude);
            if (distance > request.RadiusMeters)
            {
                continue;
            }

            var spottedPayload = new
            {
                requestId = request.Id,
                ownerUserId = request.CreatedByUserId,
                requestAddress = request.Address,
                latitude,
                longitude
            };
            await _notifier.NotifyUserAsync(request.CreatedByUserId, "ParkingSpottedNearRequest", spottedPayload);

            _logger.LogInformation(
                "Parking report notification sent to requester {UserId} for request {RequestId}",
                request.CreatedByUserId,
                request.Id);
        }
    }

    private (int RadiusMeters, decimal? RewardAmount, IReadOnlyList<string> PaymentMethods) ParseRequestFields(
        CreateParkingSearchRequest request)
    {
        var radiusMeters = Math.Clamp(request.RadiusMeters, MinRadiusMeters, MaxRadiusMeters);

        if (request.RewardAmount.HasValue)
        {
            if (request.RewardAmount < 0 || request.RewardAmount > MaxRewardAmount)
            {
                throw new ArgumentException(_localizer["Error_RewardRange", MaxRewardAmount]);
            }
        }

        var rewardAmount = request.RewardAmount > 0 ? request.RewardAmount : null;
        var paymentMethods = PaymentMethodCodes.Normalize(request.PaymentMethods);
        if (rewardAmount > 0 && paymentMethods.Count == 0)
        {
            throw new ArgumentException(_localizer["Error_RewardNeedsPayment"]);
        }

        if (paymentMethods.Any(PaymentMethodCodes.IsBareOther))
        {
            throw new ArgumentException(_localizer["Error_OtherPaymentEmpty"]);
        }

        return (radiusMeters, rewardAmount, paymentMethods);
    }

    private static bool IsLiveListing(ParkingRequest request) =>
        request.Status == ParkingRequestStatus.Active ||
        request.Status == ParkingRequestStatus.Reserved;

    private ParkingRequestDto MapToDto(
        ParkingRequest request,
        Guid? viewerUserId,
        int messageCount,
        int incomingMessageCount,
        int guestSentCount = 0,
        int guestThreadCount = 0,
        bool isBlockedByOwner = false)
    {
        var now = DateTime.UtcNow;
        var isOwner = viewerUserId.HasValue && viewerUserId.Value == request.CreatedByUserId;

        var canUnreserve = isOwner
            && request.Status == ParkingRequestStatus.Reserved
            && request.ExpiresAt > now;

        return ParkingRequestMapper.ToDto(
            request,
            messageCount,
            incomingMessageCount,
            _paymentLabels.Get,
            false,
            canUnreserve,
            isBlockedByOwner,
            guestThreadCount > 0);
    }

    private async Task<ParkingRequestDto> BuildDtoForViewerAsync(ParkingRequest request, Guid viewerUserId)
    {
        var ids = new[] { request.Id };
        var counts = await _messages.CountByRequestIdsAsync(ids);
        var isOwner = request.CreatedByUserId == viewerUserId;

        if (isOwner)
        {
            var incomingCounts = await _messages.CountIncomingByRequestIdsAsync(viewerUserId, ids);
            return MapToDto(
                request,
                viewerUserId,
                counts.GetValueOrDefault(request.Id),
                incomingCounts.GetValueOrDefault(request.Id));
        }

        var guestIncoming = await _messages.CountIncomingForGuestOnRequestsAsync(viewerUserId, ids);
        var guestSent = await _messages.CountGuestSentOnRequestsAsync(viewerUserId, ids);
        var guestThread = await _messages.CountThreadMessagesForGuestOnRequestsAsync(viewerUserId, ids);
        var isBlocked = await _blocks.IsBlockedAsync(request.Id, viewerUserId);
        return MapToDto(
            request,
            viewerUserId,
            counts.GetValueOrDefault(request.Id),
            guestIncoming.GetValueOrDefault(request.Id),
            guestSent.GetValueOrDefault(request.Id),
            guestThread.GetValueOrDefault(request.Id),
            isBlocked);
    }
}
