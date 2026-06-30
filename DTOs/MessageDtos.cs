namespace Spotster.DTOs;

public record RequestMessageDto(
    Guid Id,
    Guid ParkingRequestId,
    Guid SenderUserId,
    string SenderUsername,
    string Content,
    DateTime CreatedAt,
    bool IsMine);

public record SendRequestMessageDto(string Content, Guid? ReplyToUserId = null);

public record RequestConversationDto(
    Guid GuestUserId,
    string GuestUsername,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    int MessageCount,
    int IncomingFromGuestCount,
    bool IsBlocked);

public record PaymentMethodOptionDto(string Code, string Label);
