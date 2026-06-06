using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PfmApi.Data;
using PfmApi.DTOs.Dashboard;
using PfmApi.DTOs.Transactions;

namespace PfmApi.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue("sub") ?? "0");

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = GetUserId();
        var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound(new { error = "User not found." });
        if (user.Profile == null) return NotFound(new { error = "Profile not found." });

        var transactions = await _db.Transactions.Where(t => t.UserId == userId).ToListAsync();

        var now = DateTime.UtcNow;
        var monthly = transactions
            .Where(t => t.Date.Month == now.Month && t.Date.Year == now.Year)
            .ToList();

        var totalIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
        var totalExpenses = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
        var totalSavings = user.Profile.TotalSavings;
        var savingsGoal = user.Profile.SavingsGoal;

        var monthlyIncome = monthly.Where(t => t.Type == "Income").Sum(t => t.Amount);
        var monthlyExpenses = monthly.Where(t => t.Type == "Expense").Sum(t => t.Amount);

        var progress = savingsGoal <= 0 ? 0m
            : Math.Min(100, Math.Max(0, Math.Round(totalSavings / savingsGoal * 100, 2)));

        var kpiValue = monthlyIncome > 0
            ? Math.Round((monthlyIncome - monthlyExpenses) / monthlyIncome * 100, 2)
            : 0m;
        var kpiLabel = kpiValue >= 35 ? "Strong"
            : kpiValue >= 15 ? "Stable"
            : kpiValue >= 0 ? "Watch closely"
            : "Needs attention";

        var recent = transactions
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Take(5)
            .Select(t => new TransactionResponse
            {
                Id = t.Id.ToString(),
                Date = t.Date.ToString("yyyy-MM-dd"),
                Type = t.Type,
                Category = t.Category,
                Description = t.Description,
                Amount = t.Amount,
                Status = t.Status,
                CreatedAt = t.CreatedAt
            })
            .ToList();

        return Ok(new DashboardResponse
        {
            Profile = new DashboardProfile
            {
                FullName = user.FullName,
                Currency = user.Profile.Currency,
                TotalSavings = totalSavings,
                SavingsGoal = savingsGoal
            },
            Totals = new DashboardTotals
            {
                Income = totalIncome,
                Expenses = totalExpenses,
                Savings = totalSavings,
                Balance = totalIncome - totalExpenses + totalSavings
            },
            Monthly = new DashboardMonthly
            {
                Income = monthlyIncome,
                Expenses = monthlyExpenses
            },
            SavingsProgress = progress,
            Kpi = new DashboardKpi { Value = kpiValue, Label = kpiLabel },
            RecentTransactions = recent
        });
    }
}
