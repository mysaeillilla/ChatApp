
using API.Data;
using API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/users
    // GET: api/users?search=saeil
    // GET: api/users?page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var currentUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(currentUserId, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid user authentication."
            });
        }

        // Base query
        var query = _context.Users
            .AsNoTracking()
            .Where(u => u.Id != userId);

        // Search by username or email
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(u =>
                u.Username.Contains(search) ||
                u.Email.Contains(search));
        }

        // Total records
        var totalCount = await query.CountAsync(cancellationToken);

        // Pagination validation
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var users = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                ProfileImage = u.ProfileImage,
                IsOnline = u.IsOnline,
                LastSeen = u.LastSeen
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(
                (double)totalCount / pageSize),
            users
        });
    }

    // GET: api/users/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                ProfileImage = u.ProfileImage,
                IsOnline = u.IsOnline,
                LastSeen = u.LastSeen
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        return Ok(user);
    }
}

// User Response DTO
public class UserResponseDto
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? ProfileImage { get; set; }

    public bool IsOnline { get; set; }

    public DateTime? LastSeen { get; set; }
}
