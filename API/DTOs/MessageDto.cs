namespace API.DTOs;

public record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string Content,
    string MessageType,
    DateTime SentAt,
    DateTime? EditedAt,
    Guid? ReplyToMessageId);

public record ConversationDto(
    Guid Id,
    string? Name,
    bool IsGroup,
    Guid? OtherUserId,
    string? OtherUsername,
    string? OtherProfileImage,
    bool OtherIsOnline,
    MessageDto? LastMessage,
    int UnreadCount);

// Sent by the client to the hub
public record SendMessageRequest(
    Guid ConversationId,
    string Content,
    Guid? ReplyToMessageId = null,
    string MessageType = "text");

// Returned by the service after saving
public record SentMessageResult(MessageDto Message, IReadOnlyList<Guid> MemberIds);

public record DeliveredResult(Guid MessageId, Guid ConversationId, Guid SenderId, DateTime DeliveredAt);

public record ReadResult(Guid ConversationId, Guid ReaderId, DateTime ReadAt, IReadOnlyList<Guid> MessageIds, IReadOnlyList<Guid> NotifyUserIds);