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

    public AuthController(AppDbContext db, ITokenService tokens, IPasswordHasher<User> hasher)
    {
        _db = db;
        _tokens = tokens;
        _hasher = hasher;
    }

    /// <summary>Create a new account and return a JWT.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var usernameLower = username.ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Username.ToLower() == usernameLower, ct))
            return Conflict(new { error = "Username is already taken." });

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new { error = "An account with this email already exists." });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            IsOnline = false
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique index violation from a concurrent registration
            return Conflict(new { error = "Username or email is already in use." });
        }

        return CreatedAtAction(nameof(Me), null, BuildResponse(user));
    }

    /// <summary>Log in with username or email + password and return a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var identifier = request.UsernameOrEmail.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Email == identifier || u.Username.ToLower() == identifier, ct);

        // Same message for "no such user" and "wrong password" so accounts can't be enumerated
        const string invalid = "Invalid username/email or password.";

        if (user is null)
            return Unauthorized(new { error = invalid });

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = invalid });

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync(ct);
        }

        return Ok(BuildResponse(user));
    }

    /// <summary>Return the currently authenticated user (useful to validate a stored token).</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserDto(u.Id, u.Username, u.Email, u.ProfileImage))
            .FirstOrDefaultAsync(ct);

        return user is null ? Unauthorized() : Ok(user);
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = _tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt,
            new UserDto(user.Id, user.Username, user.Email, user.ProfileImage));
    }
}