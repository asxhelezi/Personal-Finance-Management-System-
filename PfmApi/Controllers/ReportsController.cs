using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PfmApi.Data;
using PfmApi.DTOs.Transactions;

namespace PfmApi.Controllers;

[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db)
    {
        _db = db;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue("sub") ?? "0");

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] string? startDate,
        [FromQuery] string? endDate)
    {
        if (!DateOnly.TryParse(startDate, out var sd))
            return BadRequest(new { error = "Invalid or missing start date." });

        if (!DateOnly.TryParse(endDate, out var ed))
            return BadRequest(new { error = "Invalid or missing end date." });

        if (sd > ed)
            return BadRequest(new { error = "Start date must be before or equal to end date." });

        var userId = GetUserId();
        var list = await _db.Transactions
            .Where(t => t.UserId == userId && t.Date >= sd && t.Date <= ed)
            .ToListAsync();

        var income = list.Where(t => t.Type == "Income").Sum(t => t.Amount);
        var expenses = list.Where(t => t.Type == "Expense").Sum(t => t.Amount);

        return Ok(new ReportResponse
        {
            StartDate = sd.ToString("yyyy-MM-dd"),
            EndDate = ed.ToString("yyyy-MM-dd"),
            Count = list.Count,
            Income = income,
            Expenses = expenses,
            Balance = income - expenses
        });
    }
}
