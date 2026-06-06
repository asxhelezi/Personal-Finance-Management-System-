namespace PfmApi.DTOs.Profile;

public class ProfileResponse
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string Occupation { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
    public decimal SavingsGoal { get; set; }
    public decimal TotalSavings { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
