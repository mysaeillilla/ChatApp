
using System.Security.Claims;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher<User> _hasher;

    public AuthController(
        AppDbContext db,
        ITokenService tokens,
        IPasswordHasher<User> hasher)
    {
        _db = db;
        _tokens = tokens;
        _hasher = hasher;
    }

    // Register
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken ct)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var usernameLower = username.ToLowerInvariant();

        if (await _db.Users.AnyAsync(
            u => u.Username.ToLower() == usernameLower, ct))
        {
            return Conflict(new { error = "Username is already taken." });
        }

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return Conflict(new
            {
                error = "An account with this email already exists."
            });
        }

        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            IsOnline = true,
            LastSeen = now
        };

        user.PasswordHash = _hasher.HashPassword(
            user, request.Password);

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                error = "Username or email is already in use."
            });
        }

        return CreatedAtAction(
            nameof(Me),
            null,
            BuildResponse(user));
    }

    // Login
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken ct)
    {
        var identifier = request.UsernameOrEmail
            .Trim()
            .ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Email == identifier ||
                 u.Username.ToLower() == identifier,
            ct);

        const string invalid =
            "Invalid username/email or password.";

        if (user is null)
            return Unauthorized(new { error = invalid });

        var result = _hasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = invalid });

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(
                user, request.Password);
        }

        // Update online status
        user.IsOnline = true;
        user.LastSeen = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(BuildResponse(user));
    }

    // Current logged-in user
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(
        CancellationToken ct)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(claim, out var userId))
            return Unauthorized();

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserDto(
                u.Id,
                u.Username,
                u.Email,
                u.ProfileImage))
            .FirstOrDefaultAsync(ct);

        return user is null
            ? Unauthorized()
            : Ok(user);
    }

    // Logout
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        CancellationToken ct)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(claim, out var userId))
            return Unauthorized();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return Unauthorized();

        user.IsOnline = false;
        user.LastSeen = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Logged out successfully."
        });
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = _tokens.CreateToken(user);

        return new AuthResponse(
            token,
            expiresAt,
            new UserDto(
                user.Id,
                user.Username,
                user.Email,
                user.ProfileImage));
    }
}
