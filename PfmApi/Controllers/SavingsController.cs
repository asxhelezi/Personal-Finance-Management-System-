using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PfmApi.Data;
using PfmApi.DTOs.Savings;

namespace PfmApi.Controllers;

[Authorize]
[ApiController]
[Route("api/savings")]
public class SavingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SavingsController(AppDbContext db)
    {
        _db = db;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue("sub") ?? "0");

    private static decimal CalcProgress(decimal totalSavings, decimal savingsGoal)
        => savingsGoal <= 0 ? 0 : Math.Min(100, Math.Max(0, Math.Round(totalSavings / savingsGoal * 100, 2)));

    [HttpPatch("add")]
    public async Task<IActionResult> Add([FromBody] AddSavingsRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest(new { error = "Amount must be greater than 0." });

        var userId = GetUserId();
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return NotFound(new { error = "Profile not found." });

        profile.TotalSavings = Math.Round(profile.TotalSavings + req.Amount, 2);
        await _db.SaveChangesAsync();

        return Ok(new SavingsResponse
        {
            TotalSavings = profile.TotalSavings,
            SavingsGoal = profile.SavingsGoal,
            Progress = CalcProgress(profile.TotalSavings, profile.SavingsGoal)
        });
    }

    [HttpPatch("edit")]
    public async Task<IActionResult> Edit([FromBody] EditSavingsRequest req)
    {
        if (req.TotalSavings < 0)
            return BadRequest(new { error = "Total savings cannot be negative." });
        if (req.SavingsGoal < 0)
            return BadRequest(new { error = "Savings goal cannot be negative." });

        var userId = GetUserId();
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return NotFound(new { error = "Profile not found." });

        profile.TotalSavings = Math.Round(req.TotalSavings, 2);
        profile.SavingsGoal = Math.Round(req.SavingsGoal, 2);
        await _db.SaveChangesAsync();

        return Ok(new SavingsResponse
        {
            TotalSavings = profile.TotalSavings,
            SavingsGoal = profile.SavingsGoal,
            Progress = CalcProgress(profile.TotalSavings, profile.SavingsGoal)
        });
    }
}
