using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PfmApi.Data;
using PfmApi.DTOs.Auth;
using PfmApi.Helpers;
using PfmApi.Models;

namespace PfmApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtHelper _jwt;

    public AuthController(AppDbContext db, JwtHelper jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(new { error = "Full name is required." });

        if (!Regex.IsMatch(req.Username ?? "", @"^[a-zA-Z0-9_]{4,20}$"))
            return BadRequest(new { error = "Username must be 4–20 characters: letters, numbers, or underscore." });

        if (!Regex.IsMatch(req.Email ?? "", @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            return BadRequest(new { error = "Enter a valid email address such as name@example.com." });

        if (!Regex.IsMatch(req.Password ?? "", @"^(?=.*[A-Za-z])(?=.*\d).{8,}$"))
            return BadRequest(new { error = "Password must be at least 8 characters with a letter and a number." });

        var emailLower = req.Email.Trim().ToLower();
        var usernameLower = req.Username.Trim().ToLower();

        if (await _db.Users.AnyAsync(u => u.Email == emailLower))
            return Conflict(new { error = "That email is already registered." });

        if (await _db.Users.AnyAsync(u => u.Username.ToLower() == usernameLower))
            return Conflict(new { error = "That username is already taken." });

        var user = new User
        {
            FullName = req.FullName.Trim(),
            Username = req.Username.Trim(),
            Email = emailLower,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, 12),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created(string.Empty, new AuthResponse
        {
            Token = _jwt.GenerateToken(user),
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            ProfileComplete = false
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!Regex.IsMatch(req.Email ?? "", @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            return BadRequest(new { error = "Enter a valid email address such as name@example.com." });

        if (string.IsNullOrEmpty(req.Password))
            return BadRequest(new { error = "Enter your password." });

        var emailLower = req.Email.Trim().ToLower();
        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Email == emailLower);

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Incorrect email or password." });

        var profileComplete = user.Profile != null && !string.IsNullOrEmpty(user.Profile.Phone);

        return Ok(new AuthResponse
        {
            Token = _jwt.GenerateToken(user),
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            ProfileComplete = profileComplete
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = int.Parse(User.FindFirstValue("sub") ?? "0");

        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(new { error = "User not found." });

        var profileComplete = user.Profile != null && !string.IsNullOrEmpty(user.Profile.Phone);

        return Ok(new
        {
            id = user.Id,
            fullName = user.FullName,
            username = user.Username,
            email = user.Email,
            createdAt = user.CreatedAt,
            profileComplete
        });
    }
}
