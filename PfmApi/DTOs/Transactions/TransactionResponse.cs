namespace PfmApi.DTOs.Transactions;

public class TransactionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class TransactionListResponse
{
    public int Count { get; set; }
    public List<TransactionResponse> Items { get; set; } = new();
}
