
using API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/attachments")]
public class AttachmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    private string StoragePath => Path.GetFullPath(
        Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "ChatAttachments"));

    public AttachmentsController(
        AppDbContext db,
        IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    private Guid? CurrentUserId
    {
        get
        {
            var claim =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            return Guid.TryParse(claim, out var id)
                ? id
                : null;
        }
    }

    private string ResolveFilePath(string relativePath)
    {
        var fullPath = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, relativePath));

        var root = StoragePath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
            root,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Invalid attachment storage path.");
        }

        return fullPath;
    }

    // GET: api/attachments/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAttachment(
        Guid id,
        CancellationToken ct)
    {
        var userId = CurrentUserId;

        if (userId == null)
            return Unauthorized();

        var attachment = await _db.Attachments
            .AsNoTracking()
            .Where(a =>
                a.Id == id &&
                a.Message.Conversation.Members.Any(
                    m => m.UserId == userId.Value))
            .Select(a => new
            {
                a.Id,
                a.MessageId,
                a.FileName,
                a.FileType,
                a.FileSize
            })
            .FirstOrDefaultAsync(ct);

        if (attachment == null)
            return NotFound();

        return Ok(new
        {
            attachment.Id,
            attachment.MessageId,
            attachment.FileName,
            attachment.FileType,
            attachment.FileSize,
            DownloadUrl = $"/api/attachments/{id}/download"
        });
    }

    // GET: api/attachments/{id}/download
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(
        Guid id,
        CancellationToken ct)
    {
        var userId = CurrentUserId;

        if (userId == null)
            return Unauthorized();

        var attachment = await _db.Attachments
            .AsNoTracking()
            .Where(a =>
                a.Id == id &&
                a.Message.Conversation.Members.Any(
                    m => m.UserId == userId.Value))
            .Select(a => new
            {
                a.FileName,
                a.FilePath
            })
            .FirstOrDefaultAsync(ct);

        if (attachment == null)
            return NotFound();

        string physicalPath;

        try
        {
            physicalPath = ResolveFilePath(attachment.FilePath);
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new
            {
                error = "Invalid attachment path."
            });
        }

        if (!System.IO.File.Exists(physicalPath))
            return NotFound(new { error = "File not found." });

        var stream = new FileStream(
            physicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);

        return File(
            stream,
            "application/octet-stream",
            Path.GetFileName(attachment.FileName),
            enableRangeProcessing: true);
    }

    // DELETE: api/attachments/{id}
    // Only the original message sender can delete an attachment.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken ct)
    {
        var userId = CurrentUserId;

        if (userId == null)
            return Unauthorized();

        var attachment = await _db.Attachments
            .Include(a => a.Message)
            .FirstOrDefaultAsync(
                a => a.Id == id,
                ct);

        if (attachment == null)
            return NotFound();

        if (attachment.Message.SenderId != userId.Value)
            return Forbid();

        string physicalPath;

        try
        {
            physicalPath = ResolveFilePath(attachment.FilePath);
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new
            {
                error = "Invalid attachment path."
            });
        }

        _db.Attachments.Remove(attachment);

        await _db.SaveChangesAsync(ct);

        if (System.IO.File.Exists(physicalPath))
            System.IO.File.Delete(physicalPath);

        return Ok(new
        {
            message = "Attachment deleted successfully."
        });
    }
}
