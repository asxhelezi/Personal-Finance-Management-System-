namespace PfmApi.Models;

public class Profile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string Occupation { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
    public decimal SavingsGoal { get; set; } = 0;
    public decimal TotalSavings { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
}
