using System.Security.Claims;
using API.DTOs;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chat;

    public ChatController(IChatService chat) => _chat = chat;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? throw new UnauthorizedAccessException());

    /// <summary>Inbox: all conversations with last message and unread count.</summary>
    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<ConversationDto>>> GetConversations(CancellationToken ct)
        => Ok(await _chat.GetConversationsAsync(CurrentUserId, ct));

    /// <summary>Get or create the 1:1 conversation with another user.</summary>
    [HttpPost("conversations/direct/{otherUserId:guid}")]
    public async Task<ActionResult<object>> StartDirect(Guid otherUserId, CancellationToken ct)
    {
        try
        {
            var id = await _chat.GetOrCreateDirectConversationAsync(CurrentUserId, otherUserId, ct);
            return Ok(new { conversationId = id });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>
    /// Message history, newest page first. Pass ?before=&lt;SentAt of oldest loaded message&gt; to page backwards.
    /// </summary>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> GetMessages(
        Guid conversationId, [FromQuery] DateTime? before, [FromQuery] int take = 30, CancellationToken ct = default)
    {
        try
        {
            return Ok(await _chat.GetMessagesAsync(CurrentUserId, conversationId, before, take, ct));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}