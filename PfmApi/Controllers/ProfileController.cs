using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PfmApi.Data;
using PfmApi.DTOs.Profile;
using PfmApi.Models;

namespace PfmApi.Controllers;

[Authorize]
[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private static readonly string[] ValidCurrencies = ["EUR", "USD", "LEK"];

    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db)
    {
        _db = db;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue("sub") ?? "0");

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();
        var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound(new { error = "User not found." });
        if (user.Profile == null) return NotFound(new { error = "Profile not found." });

        return Ok(ToResponse(user));
    }

    [HttpPut]
    public async Task<IActionResult> UpsertProfile([FromBody] UpsertProfileRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(new { error = "Full name is required." });

        if (!Regex.IsMatch(req.Username ?? "", @"^[a-zA-Z0-9_]{4,20}$"))
            return BadRequest(new { error = "Username must be 4–20 characters: letters, numbers, or underscore." });

        if (!ValidCurrencies.Contains(req.Currency))
            return BadRequest(new { error = "Invalid currency. Use EUR, USD, or LEK." });

        var userId = GetUserId();
        var usernameLower = req.Username.Trim().ToLower();

        var usernameConflict = await _db.Users
            .AnyAsync(u => u.Username.ToLower() == usernameLower && u.Id != userId);
        if (usernameConflict)
            return Conflict(new { error = "That username is already taken." });

        var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound(new { error = "User not found." });

        user.FullName = req.FullName.Trim();
        user.Username = req.Username.Trim();

        if (user.Profile == null)
        {
            user.Profile = new Profile
            {
                UserId = userId,
                Phone = req.Phone?.Trim() ?? string.Empty,
                Age = req.Age,
                Occupation = req.Occupation?.Trim() ?? string.Empty,
                Currency = req.Currency,
                SavingsGoal = req.SavingsGoal,
                TotalSavings = req.TotalSavings,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }
        else
        {
            user.Profile.Phone = req.Phone?.Trim() ?? string.Empty;
            user.Profile.Age = req.Age;
            user.Profile.Occupation = req.Occupation?.Trim() ?? string.Empty;
            user.Profile.Currency = req.Currency;
            user.Profile.SavingsGoal = req.SavingsGoal;
            user.Profile.TotalSavings = req.TotalSavings;
        }

        await _db.SaveChangesAsync();
        return Ok(ToResponse(user));
    }

    [HttpPost("convert-currency")]
    public async Task<IActionResult> ConvertCurrency([FromBody] ConvertCurrencyRequest req)
    {
        if (!ValidCurrencies.Contains(req.FromCurrency) || !ValidCurrencies.Contains(req.ToCurrency))
            return BadRequest(new { error = "Invalid currency. Use EUR, USD, or LEK." });

        if (req.Rate <= 0)
            return BadRequest(new { error = "Rate must be greater than 0." });

        var userId = GetUserId();
        var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound(new { error = "User not found." });
        if (user.Profile == null) return NotFound(new { error = "Profile not found." });

        if (user.Profile.Currency != req.FromCurrency)
            return BadRequest(new { error = $"Current currency is {user.Profile.Currency}, not {req.FromCurrency}." });

        var transactions = await _db.Transactions.Where(t => t.UserId == userId).ToListAsync();

        await using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var t in transactions)
                t.Amount = Math.Round(t.Amount * req.Rate, 2);

            user.Profile.TotalSavings = Math.Round(user.Profile.TotalSavings * req.Rate, 2);
            user.Profile.SavingsGoal = Math.Round(user.Profile.SavingsGoal * req.Rate, 2);
            user.Profile.Currency = req.ToCurrency;

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();
        }
        catch
        {
            await dbTx.RollbackAsync();
            return StatusCode(500, new { error = "Currency conversion failed. No data was changed." });
        }

        return Ok(ToResponse(user));
    }

    private static ProfileResponse ToResponse(User user) => new()
    {
        FullName = user.FullName,
        Username = user.Username,
        Email = user.Email,
        Phone = user.Profile?.Phone ?? string.Empty,
        Age = user.Profile?.Age,
        Occupation = user.Profile?.Occupation ?? string.Empty,
        Currency = user.Profile?.Currency ?? "EUR",
        SavingsGoal = user.Profile?.SavingsGoal ?? 0,
        TotalSavings = user.Profile?.TotalSavings ?? 0,
        CreatedAt = user.Profile?.CreatedAt ?? user.CreatedAt
    };
}
