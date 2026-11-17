
using System.Security.Claims;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chat;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    private const long MaxFileSize = 100 * 1024 * 1024;
    private const long MaxRequestSize = 500 * 1024 * 1024;
    private const int MaxFiles = 10;

    private string StoragePath => Path.Combine(
        _environment.ContentRootPath,
        "App_Data",
        "ChatAttachments");

    public ChatController(
        IChatService chat,
        AppDbContext db,
        IWebHostEnvironment environment)
    {
        _chat = chat;
        _db = db;
        _environment = environment;
    }

    private Guid CurrentUserId
    {
        get
        {
            var claim =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!Guid.TryParse(claim, out var id))
                throw new UnauthorizedAccessException(
                    "Invalid authentication.");

            return id;
        }
    }

    // GET: api/chat/conversations
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(
        CancellationToken ct)
    {
        return Ok(
            await _chat.GetConversationsAsync(CurrentUserId, ct));
    }

    // POST: api/chat/conversations/direct/{otherUserId}
    [HttpPost("conversations/direct/{otherUserId:guid}")]
    public async Task<IActionResult> StartDirect(
        Guid otherUserId,
        CancellationToken ct)
    {
        try
        {
            var id = await _chat.GetOrCreateDirectConversationAsync(
                CurrentUserId,
                otherUserId,
                ct);

            return Ok(new { conversationId = id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // GET: api/chat/conversations/{conversationId}/messages
    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        [FromQuery] DateTime? before,
        [FromQuery] int take = 30,
        CancellationToken ct = default)
    {
        try
        {
            var messages = await _chat.GetMessagesAsync(
                CurrentUserId,
                conversationId,
                before,
                take,
                ct);

            return Ok(messages);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // POST:
    // api/chat/conversations/{conversationId}/messages/attachments
    //
    // Form-data:
    // content         (optional)
    // replyToMessageId (optional)
    // files           (multiple files)

    [HttpPost(
        "conversations/{conversationId:guid}/messages/attachments")]
    [RequestSizeLimit(MaxRequestSize)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = MaxRequestSize)]
    public async Task<IActionResult> SendMessageWithAttachments(
        Guid conversationId,
        [FromForm] string? content,
        [FromForm] Guid? replyToMessageId,
        [FromForm] List<IFormFile>? files,
        CancellationToken ct)
    {
        var userId = CurrentUserId;

        if (files == null || files.Count == 0)
        {
            return BadRequest(new
            {
                error = "At least one attachment is required."
            });
        }

        if (files.Count > MaxFiles)
        {
            return BadRequest(new
            {
                error = $"Maximum {MaxFiles} files are allowed."
            });
        }

        long totalSize = 0;

        foreach (var file in files)
        {
            if (file.Length <= 0)
            {
                return BadRequest(new
                {
                    error = "Empty files are not allowed."
                });
            }

            if (file.Length > MaxFileSize)
            {
                return BadRequest(new
                {
                    error = "Each file must be 100 MB or smaller."
                });
            }

            totalSize += file.Length;
        }

        if (totalSize > MaxRequestSize)
        {
            return BadRequest(new
            {
                error = "Total attachment size exceeds the limit."
            });
        }

        // Validate conversation membership.
        try
        {
            await _chat.GetMemberIdsAsync(
                userId,
                conversationId,
                ct);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        // Create storage directory.
        Directory.CreateDirectory(StoragePath);

        var createdFiles = new List<string>();
        var attachments = new List<Attachment>();

        try
        {
            foreach (var file in files)
            {
                var originalName =
                    Path.GetFileName(file.FileName);

                var extension =
                    Path.GetExtension(originalName);

                // Generate a unique physical filename.
                var attachmentId = Guid.NewGuid();

                var storedName = attachmentId + extension;

                var physicalPath = Path.Combine(
                    StoragePath,
                    storedName);

                var relativePath = Path.Combine(
                    "App_Data",
                    "ChatAttachments",
                    storedName);

                await using (var stream = new FileStream(
                    physicalPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true))
                {
                    await file.CopyToAsync(stream, ct);
                }

                createdFiles.Add(physicalPath);

                attachments.Add(new Attachment
                {
                    Id = attachmentId,
                    FileName = originalName,
                    FilePath = relativePath,
                    FileType = string.IsNullOrWhiteSpace(
                        file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType,
                    FileSize = file.Length
                });
            }

            var request = new SendMessageRequest(
    conversationId,
    content?.Trim() ?? string.Empty,
    replyToMessageId,
    "file");

            // The service creates the message, receipts
            // and attachment records in one database save.
            var result = await _chat.SaveMessageWithAttachmentsAsync(
                userId,
                request,
                attachments,
                ct);

            return Ok(new
            {
                message = result.Message,
                recipientIds = result.MemberIds
            });
        }
        catch (ArgumentException ex)
        {
            CleanupFiles(createdFiles);
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            CleanupFiles(createdFiles);
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            CleanupFiles(createdFiles);
            return NotFound(new { error = ex.Message });
        }
        catch
        {
            CleanupFiles(createdFiles);
            throw;
        }
    }

    private static void CleanupFiles(
        IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch
            {
                // Log cleanup failure in production.
            }
        }
    }
}
