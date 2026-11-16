using System.Collections.Concurrent;
using API.DTOs;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.Hubs;

/// <summary>
/// Tracks how many live connections each user has (phone + laptop + several tabs...).
/// A user is "online" while they have at least one connection.
/// Register as a singleton.
/// </summary>
public class PresenceTracker
{
    private readonly ConcurrentDictionary<Guid, int> _connections = new();

    /// <returns>true if this was the user's first connection (they just came online)</returns>
    public bool UserConnected(Guid userId)
    {
        var count = _connections.AddOrUpdate(userId, 1, (_, c) => c + 1);
        return count == 1;
    }

    /// <returns>true if this was the user's last connection (they just went offline)</returns>
    public bool UserDisconnected(Guid userId)
    {
        while (true)
        {
            if (!_connections.TryGetValue(userId, out var count))
                return false;

            if (count <= 1)
            {
                if (_connections.TryRemove(new KeyValuePair<Guid, int>(userId, count)))
                    return true;
            }
            else if (_connections.TryUpdate(userId, count - 1, count))
            {
                return false;
            }
        }
    }
}

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chat;
    private readonly PresenceTracker _presence;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IChatService chat, PresenceTracker presence, ILogger<ChatHub> logger)
    {
        _chat = chat;
        _presence = presence;
        _logger = logger;
    }

    // Context.UserIdentifier = the NameIdentifier claim of the authenticated user.
    // Clients.Users(...) / Clients.User(...) route to ALL connections of that user,
    // so no manual group management is needed for 1:1 chats.
    private Guid CurrentUserId =>
        Guid.TryParse(Context.UserIdentifier, out var id)
            ? id
            : throw new HubException("Unauthenticated.");

    private static IReadOnlyList<string> ToIds(IEnumerable<Guid> ids) =>
        ids.Select(i => i.ToString()).ToList();

    // ----------------------------------------------------------------
    // Connection lifecycle + presence
    // ----------------------------------------------------------------

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;

        if (_presence.UserConnected(userId))
        {
            var lastSeen = await _chat.SetPresenceAsync(userId, true);
            await BroadcastPresence(userId, true, lastSeen);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;

        if (_presence.UserDisconnected(userId))
        {
            var lastSeen = await _chat.SetPresenceAsync(userId, false);
            await BroadcastPresence(userId, false, lastSeen);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task BroadcastPresence(Guid userId, bool isOnline, DateTime lastSeen)
    {
        var contactIds = await _chat.GetContactIdsAsync(userId);
        if (contactIds.Count == 0) return;

        await Clients.Users(ToIds(contactIds))
            .SendAsync("PresenceChanged", new { userId, isOnline, lastSeen });
    }

    // ----------------------------------------------------------------
    // Client -> Server methods
    // ----------------------------------------------------------------

    /// <summary>Open (or create) the 1:1 conversation with another user. Returns the conversation id.</summary>
    public async Task<Guid> StartDirectChat(Guid otherUserId)
    {
        try
        {
            return await _chat.GetOrCreateDirectConversationAsync(CurrentUserId, otherUserId);
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException)
        {
            throw new HubException(ex.Message);
        }
    }

    /// <summary>Validates membership, saves the message, then pushes it to both users (all their devices).</summary>
    public async Task<MessageDto> SendMessage(SendMessageRequest request)
    {
        try
        {
            var result = await _chat.SaveMessageAsync(CurrentUserId, request);

            await Clients.Users(ToIds(result.MemberIds))
                .SendAsync("ReceiveMessage", result.Message);

            return result.Message; // acknowledgement for the caller
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HubException(ex.Message);
        }
        catch (ArgumentException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    /// <summary>Recipient's client calls this when a message arrives on their device.</summary>
    public async Task MessageDelivered(Guid messageId)
    {
        var result = await _chat.MarkDeliveredAsync(CurrentUserId, messageId);
        if (result is null) return;

        await Clients.User(result.SenderId.ToString())
            .SendAsync("MessageDelivered", new
            {
                result.MessageId,
                result.ConversationId,
                DeliveredTo = CurrentUserId,
                result.DeliveredAt
            });
    }

    /// <summary>Call when the user opens a conversation or sees new messages in it.</summary>
    public async Task MarkRead(Guid conversationId)
    {
        try
        {
            var result = await _chat.MarkReadAsync(CurrentUserId, conversationId);
            if (result.MessageIds.Count == 0) return;

            // Tell the other participant(s) so they can show "seen"
            await Clients.Users(ToIds(result.NotifyUserIds))
                .SendAsync("MessagesRead", new
                {
                    result.ConversationId,
                    result.ReaderId,
                    result.ReadAt,
                    result.MessageIds
                });

            // Tell all of the reader's own devices to clear the unread badge
            await Clients.User(CurrentUserId.ToString())
                .SendAsync("ConversationRead", new { result.ConversationId });
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    /// <summary>Typing indicator. Throttle on the client (e.g. once every 2-3 seconds).</summary>
    public async Task Typing(Guid conversationId)
    {
        try
        {
            var memberIds = await _chat.GetMemberIdsAsync(CurrentUserId, conversationId);
            var others = memberIds.Where(id => id != CurrentUserId);

            await Clients.Users(ToIds(others))
                .SendAsync("UserTyping", new { conversationId, userId = CurrentUserId });
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HubException(ex.Message);
        }
    }
}