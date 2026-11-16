using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using API.Data;

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
    [HttpGet]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var currentUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        var query = _context.Users
            .AsNoTracking()
            .AsQueryable();

        // Exclude currently logged-in user
        if (Guid.TryParse(currentUserId, out var userId))
        {
            query = query.Where(u => u.Id != userId);
        }

        // Search by username or email
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.Username.Contains(search) ||
                u.Email.Contains(search));
        }

        var users = await query
            .OrderBy(u => u.Username)
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

        return Ok(users);
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
            return NotFound(new { message = "User not found." });

        return Ok(user);
    }
}

// User response DTO
public class UserResponseDto
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? ProfileImage { get; set; }

    public bool IsOnline { get; set; }

    public DateTime? LastSeen { get; set; }
}