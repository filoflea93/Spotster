using Spotster.Domain;
using Spotster.DTOs;
using Spotster.Entities;
using Spotster.Repositories;
using Spotster.Resources;
using Spotster.Services.AntiFraud;
using Spotster.Services.Localization;
using Spotster.Services.Realtime;
using Microsoft.Extensions.Localization;

namespace Spotster.Services;

public interface IRequestMessagingService
{
    IReadOnlyList<PaymentMethodOptionDto> GetPaymentMethodOptions();
    Task<IReadOnlyList<RequestConversationDto>> GetConversationsAsync(Guid requestId, Guid userId);
    Task<IReadOnlyList<RequestMessageDto>> GetMessagesAsync(Guid requestId, Guid userId, Guid? withUserId);
    Task<RequestMessageDto> SendMessageAsync(Guid requestId, Guid userId, string content, Guid? replyToUserId);
    Task<RequestMessageDto> SendPhotoMessageAsync(Guid requestId, Guid userId, IFormFile photo, Guid? replyToUserId);
}

public class RequestMessagingService : IRequestMessagingService
{
    private const int MaxMessageLength = 500;

    private readonly IParkingRequestRepository _requests;
    private readonly IParkingRequestMessageRepository _messages;
    private readonly IParkingRequestBlockRepository _blocks;
    private readonly IUserRepository _users;
    private readonly IPhotoStorageService _photoStorage;
    private readonly IAntiFraudService _antiFraud;
    private readonly IParkingRealtimeNotifier _notifier;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly PaymentMethodLabels _paymentLabels;

    public RequestMessagingService(
        IParkingRequestRepository requests,
        IParkingRequestMessageRepository messages,
        IParkingRequestBlockRepository blocks,
        IUserRepository users,
        IPhotoStorageService photoStorage,
        IAntiFraudService antiFraud,
        IParkingRealtimeNotifier notifier,
        IStringLocalizer<SharedResources> localizer,
        PaymentMethodLabels paymentLabels)
    {
        _requests = requests;
        _messages = messages;
        _blocks = blocks;
        _users = users;
        _photoStorage = photoStorage;
        _antiFraud = antiFraud;
        _notifier = notifier;
        _localizer = localizer;
        _paymentLabels = paymentLabels;
    }

    public IReadOnlyList<PaymentMethodOptionDto> GetPaymentMethodOptions() =>
        PaymentMethodCodes.All
            .Select(code => new PaymentMethodOptionDto(code, _paymentLabels.Get(code)))
            .ToList();

    public async Task<IReadOnlyList<RequestConversationDto>> GetConversationsAsync(Guid requestId, Guid userId)
    {
        var request = await GetRequestOrThrowAsync(requestId);
        if (request.CreatedByUserId != userId)
        {
            throw new UnauthorizedAccessException(_localizer["Error_OwnerOnlyConversations"]);
        }

        var summaries = await _messages.GetThreadSummariesAsync(requestId);
        var blockedGuestIds = await _blocks.GetBlockedGuestIdsAsync(requestId);
        return summaries
            .Select(s => new RequestConversationDto(
                s.GuestUserId,
                s.GuestUsername,
                FormatMessagePreview(s.LastMessagePreview),
                s.LastMessageAt,
                s.MessageCount,
                s.IncomingFromGuestCount,
                blockedGuestIds.Contains(s.GuestUserId)))
            .ToList();
    }

    private string FormatMessagePreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        if (ChatLocationMessage.IsLocationMessage(content))
        {
            return _localizer["Chat_LocationPreview"];
        }

        if (ChatPhotoMessage.IsPhotoMessage(content))
        {
            return _localizer["Chat_PhotoPreview"];
        }

        return content.Length > 60 ? content[..60] + "…" : content;
    }

    public async Task<IReadOnlyList<RequestMessageDto>> GetMessagesAsync(
        Guid requestId,
        Guid userId,
        Guid? withUserId)
    {
        var request = await GetRequestOrThrowAsync(requestId);
        var threadGuestId = ResolveThreadGuestId(request, userId, withUserId, _localizer);
        await EnsureCanAccessThreadAsync(request, userId, threadGuestId);

        var items = await _messages.GetByRequestAndGuestAsync(requestId, threadGuestId);
        return items.Select(m => ToDto(m, userId)).ToList();
    }

    public async Task<RequestMessageDto> SendMessageAsync(
        Guid requestId,
        Guid userId,
        string content,
        Guid? replyToUserId)
    {
        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorized"]);
        }

        content = content.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(_localizer["Error_MessageEmpty"]);
        }

        if (content.Length > MaxMessageLength)
        {
            throw new ArgumentException(_localizer["Error_MessageTooLong", MaxMessageLength]);
        }

        var (request, threadGuestId) = await ValidateMessageSendAsync(requestId, userId, replyToUserId);
        return await PersistAndBroadcastMessageAsync(request, userId, threadGuestId, Guid.NewGuid(), content);
    }

    public async Task<RequestMessageDto> SendPhotoMessageAsync(
        Guid requestId,
        Guid userId,
        IFormFile photo,
        Guid? replyToUserId)
    {
        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorized"]);
        }

        if (photo is null || photo.Length == 0)
        {
            throw new ArgumentException(_localizer["Error_PhotoRequired"]);
        }

        var (request, threadGuestId) = await ValidateMessageSendAsync(requestId, userId, replyToUserId);
        var messageId = Guid.NewGuid();
        var photoUrl = await _photoStorage.SaveChatPhotoAsync(photo, messageId);
        var content = ChatPhotoMessage.Format(photoUrl);
        return await PersistAndBroadcastMessageAsync(request, userId, threadGuestId, messageId, content);
    }

    private async Task<(ParkingRequest request, Guid threadGuestId)> ValidateMessageSendAsync(
        Guid requestId,
        Guid userId,
        Guid? replyToUserId)
    {
        var request = await GetRequestOrThrowAsync(requestId);
        var isOwner = request.CreatedByUserId == userId;
        Guid threadGuestId;

        if (isOwner)
        {
            if (!replyToUserId.HasValue || replyToUserId.Value == userId)
            {
                throw new ArgumentException(_localizer["Error_ReplyToRequired"]);
            }

            threadGuestId = replyToUserId.Value;
            var threadMessages = await _messages.GetByRequestAndGuestAsync(requestId, threadGuestId);
            if (!threadMessages.Any(m => m.SenderUserId == threadGuestId))
            {
                throw new InvalidOperationException(_localizer["Error_NoConversation"]);
            }
        }
        else
        {
            threadGuestId = userId;
            if (!IsLiveRequest(request))
            {
                var threadMessages = await _messages.GetByRequestAndGuestAsync(requestId, threadGuestId);
                if (threadMessages.Count == 0)
                {
                    throw new InvalidOperationException(_localizer["Error_RequestInactive"]);
                }
            }
        }

        EnsureMessagingAllowedForReservation(request, userId, threadGuestId);
        await EnsureCanAccessThreadAsync(request, userId, threadGuestId);

        if (!isOwner && await _blocks.IsBlockedAsync(requestId, threadGuestId))
        {
            throw new InvalidOperationException(_localizer["Error_UserBlockedOnRequest"]);
        }

        return (request, threadGuestId);
    }

    private async Task<RequestMessageDto> PersistAndBroadcastMessageAsync(
        ParkingRequest request,
        Guid userId,
        Guid threadGuestId,
        Guid messageId,
        string content)
    {
        var message = new ParkingRequestMessage
        {
            Id = messageId,
            ParkingRequestId = request.Id,
            SenderUserId = userId,
            GuestUserId = threadGuestId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        await _messages.AddAsync(message);
        var sender = await _users.GetByIdAsync(userId);
        message.SenderUser = sender ?? new User { Id = userId, UserName = "user" };

        await _messages.SaveChangesAsync();

        var dto = ToDto(message, userId);
        var isOwner = request.CreatedByUserId == userId;
        var recipientUserId = isOwner ? threadGuestId : request.CreatedByUserId;
        var messagePayload = new
        {
            requestId = request.Id,
            ownerUserId = request.CreatedByUserId,
            threadGuestUserId = threadGuestId,
            recipientUserId,
            address = request.Address,
            message = dto
        };
        await _notifier.NotifyRequestAsync(request.Id, "RequestMessageReceived", messagePayload);
        await _notifier.NotifyUserAsync(recipientUserId, "RequestMessageReceived", messagePayload);

        return dto;
    }

    private async Task<ParkingRequest> GetRequestOrThrowAsync(Guid requestId) =>
        await _requests.GetByIdAsync(requestId)
            ?? throw new KeyNotFoundException(_localizer["Error_RequestNotFound"]);

    private static Guid ResolveThreadGuestId(
        ParkingRequest request,
        Guid userId,
        Guid? withUserId,
        IStringLocalizer<SharedResources> localizer)
    {
        if (request.CreatedByUserId == userId)
        {
            if (!withUserId.HasValue || withUserId.Value == userId)
            {
                throw new ArgumentException(localizer["Error_ThreadUserRequired"]);
            }

            return withUserId.Value;
        }

        return userId;
    }

    private async Task EnsureCanAccessThreadAsync(
        ParkingRequest request,
        Guid userId,
        Guid threadGuestId)
    {
        if (threadGuestId == userId && userId == request.CreatedByUserId)
        {
            throw new UnauthorizedAccessException(_localizer["Error_InvalidConversation"]);
        }

        if (request.CreatedByUserId == userId)
        {
            return;
        }

        if (threadGuestId != userId)
        {
            throw new UnauthorizedAccessException(_localizer["Error_ConversationAccessDenied"]);
        }

        var threadMessages = await _messages.GetByRequestAndGuestAsync(request.Id, threadGuestId);
        if (threadMessages.Count > 0)
        {
            return;
        }

        if (await _blocks.IsBlockedAsync(request.Id, threadGuestId))
        {
            throw new UnauthorizedAccessException(_localizer["Error_UserBlockedOnRequest"]);
        }

        if (request.Status == ParkingRequestStatus.Active && request.ExpiresAt > DateTime.UtcNow)
        {
            return;
        }

        if (request.Status == ParkingRequestStatus.Reserved
            && request.ExpiresAt > DateTime.UtcNow
            && request.ReservedByUserId == threadGuestId)
        {
            return;
        }

        throw new UnauthorizedAccessException(_localizer["Error_ConversationAccessDenied"]);
    }

    private void EnsureMessagingAllowedForReservation(
        ParkingRequest request,
        Guid userId,
        Guid threadGuestId)
    {
        if (request.Status != ParkingRequestStatus.Reserved || request.ExpiresAt <= DateTime.UtcNow)
        {
            return;
        }

        if (!request.ReservedByUserId.HasValue)
        {
            return;
        }

        var reservedGuestId = request.ReservedByUserId.Value;
        var isOwner = request.CreatedByUserId == userId;

        if (isOwner)
        {
            if (threadGuestId != reservedGuestId)
            {
                throw new InvalidOperationException(_localizer["Error_RequestReserved"]);
            }

            return;
        }

        if (userId != reservedGuestId)
        {
            throw new InvalidOperationException(_localizer["Error_RequestReserved"]);
        }
    }

    private static bool IsLiveRequest(ParkingRequest request) =>
        request.ExpiresAt > DateTime.UtcNow &&
        (request.Status == ParkingRequestStatus.Active ||
         request.Status == ParkingRequestStatus.Reserved);

    private static RequestMessageDto ToDto(ParkingRequestMessage message, Guid currentUserId) =>
        new(
            message.Id,
            message.ParkingRequestId,
            message.SenderUserId,
            message.SenderUser?.UserName ?? "user",
            message.Content,
            message.CreatedAt,
            message.SenderUserId == currentUserId);
}
