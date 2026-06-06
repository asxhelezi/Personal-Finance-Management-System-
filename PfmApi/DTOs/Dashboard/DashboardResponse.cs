using PfmApi.DTOs.Transactions;

namespace PfmApi.DTOs.Dashboard;

public class DashboardProfile
{
    public string FullName { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
    public decimal TotalSavings { get; set; }
    public decimal SavingsGoal { get; set; }
}

public class DashboardTotals
{
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Savings { get; set; }
    public decimal Balance { get; set; }
}

public class DashboardMonthly
{
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
}

public class DashboardKpi
{
    public decimal Value { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class DashboardResponse
{
    public DashboardProfile Profile { get; set; } = new();
    public DashboardTotals Totals { get; set; } = new();
    public DashboardMonthly Monthly { get; set; } = new();
    public decimal SavingsProgress { get; set; }
    public DashboardKpi Kpi { get; set; } = new();
    public List<TransactionResponse> RecentTransactions { get; set; } = new();
}
