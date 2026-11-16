using API.Data;
using API.DTOs;
using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public interface IChatService
{
    Task<Guid> GetOrCreateDirectConversationAsync(Guid userId, Guid otherUserId, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid userId, Guid conversationId, DateTime? before, int take, CancellationToken ct = default);
    Task<SentMessageResult> SaveMessageAsync(Guid senderId, SendMessageRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetMemberIdsAsync(Guid userId, Guid conversationId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetContactIdsAsync(Guid userId, CancellationToken ct = default);
    Task<DeliveredResult?> MarkDeliveredAsync(Guid userId, Guid messageId, CancellationToken ct = default);
    Task<ReadResult> MarkReadAsync(Guid userId, Guid conversationId, CancellationToken ct = default);
    Task<DateTime> SetPresenceAsync(Guid userId, bool isOnline, CancellationToken ct = default);
}

public class ChatService : IChatService
{
    private const int MaxContentLength = 4000;
    private readonly AppDbContext _db;

    public ChatService(AppDbContext db) => _db = db;

    // ---------------------------------------------------------------
    // Conversations
    // ---------------------------------------------------------------

    public async Task<Guid> GetOrCreateDirectConversationAsync(Guid userId, Guid otherUserId, CancellationToken ct = default)
    {
        if (userId == otherUserId)
            throw new ArgumentException("You cannot start a conversation with yourself.");

        if (!await _db.Users.AnyAsync(u => u.Id == otherUserId, ct))
            throw new KeyNotFoundException("User not found.");

        var existingId = await _db.Conversations
            .Where(c => !c.IsGroup
                        && c.Members.Count == 2
                        && c.Members.Any(m => m.UserId == userId)
                        && c.Members.Any(m => m.UserId == otherUserId))
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

        if (existingId.HasValue)
            return existingId.Value;

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            IsGroup = false,
            CreatedAt = now
        };

        foreach (var id in new[] { userId, otherUserId })
        {
            conversation.Members.Add(new ConversationMember
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                UserId = id,
                JoinedAt = now
            });
        }

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(ct);
        return conversation.Id;
    }

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.Conversation.Id,
                m.Conversation.Name,
                m.Conversation.IsGroup,
                Other = m.Conversation.Members
                    .Where(x => x.UserId != userId)
                    .Select(x => new { x.User.Id, x.User.Username, x.User.ProfileImage, x.User.IsOnline })
                    .FirstOrDefault(),
                Last = m.Conversation.Messages
                    .OrderByDescending(x => x.SentAt)
                    .Select(x => new
                    {
                        x.Id,
                        x.SenderId,
                        SenderName = x.Sender.Username,
                        x.Content,
                        x.MessageType,
                        x.SentAt,
                        x.EditedAt,
                        x.ReplyToMessageId
                    })
                    .FirstOrDefault(),
                Unread = _db.MessageReceipts.Count(r =>
                    r.UserId == userId && r.ReadAt == null && r.Message.ConversationId == m.ConversationId)
            })
            .ToListAsync(ct);

        return rows
            .OrderByDescending(r => r.Last != null ? r.Last.SentAt : DateTime.MinValue)
            .Select(r => new ConversationDto(
                r.Id,
                r.Name,
                r.IsGroup,
                r.Other?.Id,
                r.Other?.Username,
                r.Other?.ProfileImage,
                r.Other?.IsOnline ?? false,
                r.Last == null
                    ? null
                    : new MessageDto(r.Last.Id, r.Id, r.Last.SenderId, r.Last.SenderName, r.Last.Content,
                        r.Last.MessageType, r.Last.SentAt, r.Last.EditedAt, r.Last.ReplyToMessageId),
                r.Unread))
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetMemberIdsAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
    {
        var memberIds = await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        if (!memberIds.Contains(userId))
            throw new UnauthorizedAccessException("You are not a member of this conversation.");

        return memberIds;
    }

    /// <summary>Everyone who shares at least one conversation with the user (used for presence updates).</summary>
    public async Task<IReadOnlyList<Guid>> GetContactIdsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.UserId != userId
                        && m.Conversation.Members.Any(x => x.UserId == userId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    // ---------------------------------------------------------------
    // Messages
    // ---------------------------------------------------------------

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(
        Guid userId, Guid conversationId, DateTime? before, int take, CancellationToken ct = default)
    {
        await GetMemberIdsAsync(userId, conversationId, ct); // membership check

        take = Math.Clamp(take, 1, 100);

        var query = _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        if (before.HasValue)
            query = query.Where(m => m.SentAt < before.Value);

        var items = await query
            .OrderByDescending(m => m.SentAt)
            .Take(take)
            .Select(m => new MessageDto(
                m.Id, m.ConversationId, m.SenderId, m.Sender.Username, m.Content,
                m.MessageType, m.SentAt, m.EditedAt, m.ReplyToMessageId))
            .ToListAsync(ct);

        items.Reverse(); // oldest -> newest for display
        return items;
    }

    public async Task<SentMessageResult> SaveMessageAsync(Guid senderId, SendMessageRequest request, CancellationToken ct = default)
    {
        var content = request.Content?.Trim();
        if (string.IsNullOrEmpty(content))
            throw new ArgumentException("Message cannot be empty.");
        if (content.Length > MaxContentLength)
            throw new ArgumentException($"Message cannot exceed {MaxContentLength} characters.");

        var memberIds = await GetMemberIdsAsync(senderId, request.ConversationId, ct);

        if (request.ReplyToMessageId.HasValue)
        {
            var validReply = await _db.Messages.AnyAsync(
                m => m.Id == request.ReplyToMessageId && m.ConversationId == request.ConversationId, ct);
            if (!validReply)
                throw new ArgumentException("The message you are replying to was not found in this conversation.");
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = request.ConversationId,
            SenderId = senderId,
            Content = content,
            MessageType = string.IsNullOrWhiteSpace(request.MessageType) ? "text" : request.MessageType,
            SentAt = DateTime.UtcNow,
            ReplyToMessageId = request.ReplyToMessageId
        };

        // One receipt per recipient (not for the sender)
        foreach (var recipientId in memberIds.Where(id => id != senderId))
        {
            message.Receipts.Add(new MessageReceipt
            {
                Id = Guid.NewGuid(),
                MessageId = message.Id,
                UserId = recipientId
            });
        }

        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);

        var senderName = await _db.Users
            .Where(u => u.Id == senderId)
            .Select(u => u.Username)
            .FirstAsync(ct);

        var dto = new MessageDto(message.Id, message.ConversationId, senderId, senderName, message.Content,
            message.MessageType, message.SentAt, message.EditedAt, message.ReplyToMessageId);

        return new SentMessageResult(dto, memberIds);
    }

    // ---------------------------------------------------------------
    // Receipts
    // ---------------------------------------------------------------

    public async Task<DeliveredResult?> MarkDeliveredAsync(Guid userId, Guid messageId, CancellationToken ct = default)
    {
        var pending = _db.MessageReceipts
            .Where(r => r.MessageId == messageId && r.UserId == userId && r.DeliveredAt == null);

        var info = await pending
            .Select(r => new { r.Message.SenderId, r.Message.ConversationId })
            .FirstOrDefaultAsync(ct);

        if (info is null)
            return null; // already delivered, or not a recipient

        var now = DateTime.UtcNow;
        await pending.ExecuteUpdateAsync(s => s.SetProperty(r => r.DeliveredAt, (DateTime?)now), ct);

        return new DeliveredResult(messageId, info.ConversationId, info.SenderId, now);
    }

    public async Task<ReadResult> MarkReadAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
    {
        var memberIds = await GetMemberIdsAsync(userId, conversationId, ct);

        var unread = _db.MessageReceipts
            .Where(r => r.UserId == userId && r.ReadAt == null && r.Message.ConversationId == conversationId);

        var messageIds = await unread.Select(r => r.MessageId).ToListAsync(ct);
        var now = DateTime.UtcNow;

        if (messageIds.Count > 0)
        {
            await unread.ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ReadAt, (DateTime?)now)
                .SetProperty(r => r.DeliveredAt, r => r.DeliveredAt ?? now), ct);
        }

        return new ReadResult(conversationId, userId, now, messageIds,
            memberIds.Where(id => id != userId).ToList());
    }

    // ---------------------------------------------------------------
    // Presence
    // ---------------------------------------------------------------

    public async Task<DateTime> SetPresenceAsync(Guid userId, bool isOnline, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.IsOnline, isOnline)
                .SetProperty(u => u.LastSeen, (DateTime?)now), ct);
        return now;
    }
}