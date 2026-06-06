using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PfmApi.Data;
using PfmApi.DTOs.Transactions;
using PfmApi.Models;

namespace PfmApi.Controllers;

[Authorize]
[ApiController]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private static readonly string[] IncomeCategories =
        ["Allowance", "Grants", "Scholarships", "Salary", "Freelance", "Gift", "Investment", "Other Income"];
    private static readonly string[] ExpenseCategories =
        ["Groceries", "Entertainment", "Utilities", "Tuition", "Transport", "Housing", "Health", "Dining", "Other Expense"];
    private static readonly string[] ValidStatuses =
        ["Completed", "Pending", "Verified", "Flagged"];

    private readonly AppDbContext _db;

    public TransactionsController(AppDbContext db)
    {
        _db = db;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue("sub") ?? "0");

    private static TransactionResponse Map(Transaction t) => new()
    {
        Id = t.Id.ToString(),
        Date = t.Date.ToString("yyyy-MM-dd"),
        Type = t.Type,
        Category = t.Category,
        Description = t.Description,
        Amount = t.Amount,
        Status = t.Status,
        CreatedAt = t.CreatedAt
    };

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] string? category,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate)
    {
        var userId = GetUserId();
        var query = _db.Transactions.Where(t => t.UserId == userId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(t => t.Type == type);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(t => t.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t =>
                EF.Functions.ILike(t.Description, $"%{search}%") ||
                EF.Functions.ILike(t.Category, $"%{search}%"));

        if (!string.IsNullOrWhiteSpace(startDate) && DateOnly.TryParse(startDate, out var sd))
            query = query.Where(t => t.Date >= sd);

        if (!string.IsNullOrWhiteSpace(endDate) && DateOnly.TryParse(endDate, out var ed))
            query = query.Where(t => t.Date <= ed);

        var raw = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync();

        var items = raw.Select(Map).ToList();
        return Ok(new TransactionListResponse { Count = items.Count, Items = items });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TransactionRequest req)
    {
        var err = Validate(req);
        if (err != null) return BadRequest(new { error = err });

        var userId = GetUserId();
        var t = new Transaction
        {
            UserId = userId,
            Date = DateOnly.Parse(req.Date),
            Type = req.Type,
            Category = req.Category,
            Description = req.Description.Trim(),
            Amount = Math.Round(req.Amount, 2),
            Status = req.Status,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Transactions.Add(t);
        await _db.SaveChangesAsync();

        return Created(string.Empty, Map(t));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var userId = GetUserId();
        var t = await _db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (t == null) return NotFound(new { error = "Transaction not found." });
        return Ok(Map(t));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] TransactionRequest req)
    {
        var err = Validate(req);
        if (err != null) return BadRequest(new { error = err });

        var userId = GetUserId();
        var t = await _db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (t == null) return NotFound(new { error = "Transaction not found." });

        t.Date = DateOnly.Parse(req.Date);
        t.Type = req.Type;
        t.Category = req.Category;
        t.Description = req.Description.Trim();
        t.Amount = Math.Round(req.Amount, 2);
        t.Status = req.Status;

        await _db.SaveChangesAsync();
        return Ok(Map(t));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var userId = GetUserId();
        var t = await _db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (t == null) return NotFound(new { error = "Transaction not found." });

        _db.Transactions.Remove(t);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(TransactionRequest req)
    {
        if (req.Type != "Income" && req.Type != "Expense")
            return "Type must be Income or Expense.";

        var validCats = req.Type == "Income" ? IncomeCategories : ExpenseCategories;
        if (string.IsNullOrWhiteSpace(req.Category) || !validCats.Contains(req.Category))
            return "Select a category.";

        if (string.IsNullOrWhiteSpace(req.Description))
            return "Enter a description.";

        if (req.Amount <= 0)
            return "Amount must be greater than 0.";

        if (!ValidStatuses.Contains(req.Status))
            return "Invalid status.";

        if (!DateOnly.TryParse(req.Date, out _))
            return "Invalid date.";

        return null;
    }
}
