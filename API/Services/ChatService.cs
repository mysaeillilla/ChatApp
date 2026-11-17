
using API.Data;
using API.DTOs;
using API.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace API.Services;

public interface IChatService
{
    Task<Guid> GetOrCreateDirectConversationAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(
        Guid userId,
        Guid conversationId,
        DateTime? before,
        int take,
        CancellationToken ct = default);

    Task<SentMessageResult> SaveMessageAsync(
        Guid senderId,
        SendMessageRequest request,
        CancellationToken ct = default);

    Task<SentMessageResult> SaveMessageWithAttachmentsAsync(
        Guid senderId,
        SendMessageRequest request,
        IReadOnlyCollection<Attachment> attachments,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetMemberIdsAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetContactIdsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<DeliveredResult?> MarkDeliveredAsync(
        Guid userId,
        Guid messageId,
        CancellationToken ct = default);

    Task<ReadResult> MarkReadAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken ct = default);

    Task<DateTime> SetPresenceAsync(
        Guid userId,
        bool isOnline,
        CancellationToken ct = default);
}

public class ChatService : IChatService
{
    private const int MaxContentLength = 4000;

    private readonly AppDbContext _db;

    public ChatService(AppDbContext db)
    {
        _db = db;
    }

    // -------------------------------------------------------
    // Conversations
    // -------------------------------------------------------

    public async Task<Guid> GetOrCreateDirectConversationAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken ct = default)
    {
        if (userId == otherUserId)
        {
            throw new ArgumentException(
                "You cannot start a conversation with yourself.");
        }

        var currentUserExists = await _db.Users
            .AnyAsync(u => u.Id == userId, ct);

        if (!currentUserExists)
        {
            throw new KeyNotFoundException("Current user not found.");
        }

        var otherUserExists = await _db.Users
            .AnyAsync(u => u.Id == otherUserId, ct);

        if (!otherUserExists)
        {
            throw new KeyNotFoundException("User not found.");
        }

        // Find an existing direct conversation.
        var existingId = await _db.Conversations
            .Where(c =>
                !c.IsGroup &&
                c.Members.Count() == 2 &&
                c.Members.Any(m => m.UserId == userId) &&
                c.Members.Any(m => m.UserId == otherUserId))
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var now = DateTime.UtcNow;

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            IsGroup = false,
            CreatedAt = now
        };

        conversation.Members.Add(new ConversationMember
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            UserId = userId,
            JoinedAt = now
        });

        conversation.Members.Add(new ConversationMember
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            UserId = otherUserId,
            JoinedAt = now
        });

        _db.Conversations.Add(conversation);

        await _db.SaveChangesAsync(ct);

        return conversation.Id;
    }

    // -------------------------------------------------------
    // Get conversations
    // -------------------------------------------------------

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var rows = await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.Conversation.Id,
                m.Conversation.Name,
                m.Conversation.IsGroup,

                // Other conversation member.
                Other = m.Conversation.Members
                    .Where(x => x.UserId != userId)
                    .Select(x => new
                    {
                        x.User.Id,
                        x.User.Username,
                        x.User.ProfileImage,
                        x.User.IsOnline
                    })
                    .FirstOrDefault(),

                // Latest message in this conversation.
                LatestMessage = m.Conversation.Messages
                    .OrderByDescending(x => x.SentAt)
                    .ThenByDescending(x => x.Id)
                    .Select(x => new
                    {
                        x.Id,
                        x.SenderId,
                        SenderName = x.Sender.Username,
                        x.Content,
                        x.MessageType,
                        x.SentAt,
                        x.EditedAt,
                        x.ReplyToMessageId,

                        Attachments = x.Attachments
                            .Select(a => new AttachmentDto
                            {
                                Id = a.Id,
                                FileName = a.FileName,
                                FileType = a.FileType,
                                FileSize = a.FileSize,
                                DownloadUrl =
                                    $"/api/attachments/{a.Id}/download"
                            })
                            .ToList()
                    })
                    .FirstOrDefault(),

                // Number of unread messages for this user.
                Unread = _db.MessageReceipts.Count(r =>
                    r.UserId == userId &&
                    r.ReadAt == null &&
                    r.Message.ConversationId == m.ConversationId)
            })
            .ToListAsync(ct);

        return rows
            .OrderByDescending(r =>
                r.LatestMessage != null
                    ? r.LatestMessage.SentAt
                    : DateTime.MinValue)
            .Select(r => new ConversationDto(
                r.Id,
                r.Name,
                r.IsGroup,
                r.Other?.Id,
                r.Other?.Username,
                r.Other?.ProfileImage,
                r.Other?.IsOnline ?? false,

                r.LatestMessage == null
                    ? null
                    : new MessageDto(
                        r.LatestMessage.Id,
                        r.Id,
                        r.LatestMessage.SenderId,
                        r.LatestMessage.SenderName,
                        r.LatestMessage.Content,
                        r.LatestMessage.MessageType,
                        r.LatestMessage.SentAt,
                        r.LatestMessage.EditedAt,
                        r.LatestMessage.ReplyToMessageId)
                    {
                        Attachments = r.LatestMessage.Attachments
                    },

                r.Unread))
            .ToList();
    }

    // -------------------------------------------------------
    // Membership
    // -------------------------------------------------------

    public async Task<IReadOnlyList<Guid>> GetMemberIdsAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken ct = default)
    {
        var memberIds = await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        if (!memberIds.Contains(userId))
        {
            throw new UnauthorizedAccessException(
                "You are not a member of this conversation.");
        }

        return memberIds;
    }

    // -------------------------------------------------------
    // Contacts
    // -------------------------------------------------------

    public async Task<IReadOnlyList<Guid>> GetContactIdsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _db.ConversationMembers
            .AsNoTracking()
            .Where(m =>
                m.UserId != userId &&
                m.Conversation.Members.Any(x => x.UserId == userId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    // -------------------------------------------------------
    // Get messages
    // -------------------------------------------------------

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(
        Guid userId,
        Guid conversationId,
        DateTime? before,
        int take,
        CancellationToken ct = default)
    {
        // Verify that the current user belongs to this conversation.
        await GetMemberIdsAsync(userId, conversationId, ct);

        take = Math.Clamp(take, 1, 100);

        var query = _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        if (before.HasValue)
        {
            query = query.Where(m => m.SentAt < before.Value);
        }

        var items = await query
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .Take(take)
            .Select(m => new MessageDto(
                m.Id,
                m.ConversationId,
                m.SenderId,
                m.Sender.Username,
                m.Content,
                m.MessageType,
                m.SentAt,
                m.EditedAt,
                m.ReplyToMessageId)
            {
                Attachments = m.Attachments
                    .Select(a => new AttachmentDto
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        FileType = a.FileType,
                        FileSize = a.FileSize,
                        DownloadUrl =
                            $"/api/attachments/{a.Id}/download"
                    })
                    .ToList()
            })
            .ToListAsync(ct);

        // Return messages in chronological order.
        items.Reverse();

        return items;
    }

    // -------------------------------------------------------
    // Save normal text message
    // -------------------------------------------------------

    public Task<SentMessageResult> SaveMessageAsync(
        Guid senderId,
        SendMessageRequest request,
        CancellationToken ct = default)
    {
        return SaveMessageInternalAsync(
            senderId,
            request,
            Array.Empty<Attachment>(),
            ct);
    }

    // -------------------------------------------------------
    // Save message with attachments
    // -------------------------------------------------------

    public Task<SentMessageResult> SaveMessageWithAttachmentsAsync(
        Guid senderId,
        SendMessageRequest request,
        IReadOnlyCollection<Attachment> attachments,
        CancellationToken ct = default)
    {
        if (attachments == null || attachments.Count == 0)
        {
            throw new ArgumentException(
                "At least one attachment is required.");
        }

        return SaveMessageInternalAsync(
            senderId,
            request,
            attachments,
            ct);
    }

    // -------------------------------------------------------
    // Common message saving logic
    // -------------------------------------------------------

    private async Task<SentMessageResult> SaveMessageInternalAsync(
        Guid senderId,
        SendMessageRequest request,
        IReadOnlyCollection<Attachment> attachments,
        CancellationToken ct)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var content = request.Content?.Trim() ?? string.Empty;

        var messageType = string.IsNullOrWhiteSpace(request.MessageType)
            ? "text"
            : request.MessageType.Trim();

        // Empty content is allowed only when attachments exist.
        if (string.IsNullOrWhiteSpace(content) && attachments.Count == 0)
        {
            throw new ArgumentException(
                "Message content cannot be empty.");
        }

        if (content.Length > MaxContentLength)
        {
            throw new ArgumentException(
                $"Message cannot exceed {MaxContentLength} characters.");
        }

        // Verify sender membership and get all conversation members.
        var memberIds = await GetMemberIdsAsync(
            senderId,
            request.ConversationId,
            ct);

        // Validate reply target.
        if (request.ReplyToMessageId.HasValue)
        {
            var validReply = await _db.Messages.AnyAsync(
                m =>
                    m.Id == request.ReplyToMessageId.Value &&
                    m.ConversationId == request.ConversationId,
                ct);

            if (!validReply)
            {
                throw new ArgumentException(
                    "The message you are replying to was not found in this conversation.");
            }
        }

        var now = DateTime.UtcNow;

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = request.ConversationId,
            SenderId = senderId,
            Content = content,
            MessageType = messageType,
            SentAt = now,
            ReplyToMessageId = request.ReplyToMessageId
        };

        // Create delivery/read receipts for recipients.
        var receipts = memberIds
            .Where(id => id != senderId)
            .Select(recipientId => new MessageReceipt
            {
                Id = Guid.NewGuid(),
                MessageId = message.Id,
                UserId = recipientId
            })
            .ToList();

        // Associate attachment metadata with the new message.
        var attachmentList = attachments.ToList();

        foreach (var attachment in attachmentList)
        {
            attachment.Id = attachment.Id == Guid.Empty
                ? Guid.NewGuid()
                : attachment.Id;

            attachment.MessageId = message.Id;
        }

        _db.Messages.Add(message);

        if (receipts.Count > 0)
        {
            _db.MessageReceipts.AddRange(receipts);
        }

        if (attachmentList.Count > 0)
        {
            _db.Attachments.AddRange(attachmentList);
        }

        // Save message, receipts and attachments together.
        await _db.SaveChangesAsync(ct);

        var senderName = await _db.Users
            .Where(u => u.Id == senderId)
            .Select(u => u.Username)
            .FirstAsync(ct);

        var dto = new MessageDto(
            message.Id,
            message.ConversationId,
            senderId,
            senderName,
            message.Content,
            message.MessageType,
            message.SentAt,
            message.EditedAt,
            message.ReplyToMessageId)
        {
            Attachments = attachmentList
                .Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FileType = a.FileType,
                    FileSize = a.FileSize,
                    DownloadUrl =
                        $"/api/attachments/{a.Id}/download"
                })
                .ToList()
        };

        return new SentMessageResult(dto, memberIds);
    }

    // -------------------------------------------------------
    // Mark message as delivered
    // -------------------------------------------------------

    public async Task<DeliveredResult?> MarkDeliveredAsync(
        Guid userId,
        Guid messageId,
        CancellationToken ct = default)
    {
        // Load the pending receipt as a tracked entity.
        var receipt = await _db.MessageReceipts
            .Include(r => r.Message)
            .FirstOrDefaultAsync(r =>
                r.MessageId == messageId &&
                r.UserId == userId &&
                r.DeliveredAt == null,
                ct);

        if (receipt == null)
        {
            // Already delivered or user is not a recipient.
            return null;
        }

        var now = DateTime.UtcNow;

        receipt.DeliveredAt = now;

        await _db.SaveChangesAsync(ct);

        return new DeliveredResult(
            messageId,
            receipt.Message.ConversationId,
            receipt.Message.SenderId,
            now);
    }

    // -------------------------------------------------------
    // Mark conversation messages as read
    // -------------------------------------------------------

    public async Task<ReadResult> MarkReadAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken ct = default)
    {
        var memberIds = await GetMemberIdsAsync(
            userId,
            conversationId,
            ct);

        // Load unread receipts as tracked entities.
        var unread = await _db.MessageReceipts
            .Where(r =>
                r.UserId == userId &&
                r.ReadAt == null &&
                r.Message.ConversationId == conversationId)
            .ToListAsync(ct);

        var messageIds = unread
            .Select(r => r.MessageId)
            .ToList();

        var now = DateTime.UtcNow;

        foreach (var receipt in unread)
        {
            receipt.ReadAt = now;

            if (receipt.DeliveredAt == null)
            {
                receipt.DeliveredAt = now;
            }
        }

        if (unread.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        var notifyUserIds = memberIds
            .Where(id => id != userId)
            .ToList();

        return new ReadResult(
            conversationId,
            userId,
            now,
            messageIds,
            notifyUserIds);
    }

    // -------------------------------------------------------
    // User presence
    // -------------------------------------------------------

    public async Task<DateTime> SetPresenceAsync(
        Guid userId,
        bool isOnline,
        CancellationToken ct = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        var now = DateTime.UtcNow;

        user.IsOnline = isOnline;
        user.LastSeen = now;

        await _db.SaveChangesAsync(ct);

        return now;
    }
}
