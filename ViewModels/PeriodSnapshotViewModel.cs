namespace KiefProductions.ViewModels;

public class PeriodSnapshotViewModel
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // P&L
    public decimal TotalRevenue { get; set; }
    public decimal TotalCosts { get; set; }
    public decimal NetProfit { get; set; }
    public decimal ProfitMarginPercent { get; set; }

    // Accounts receivable
    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal OutstandingReceivables { get; set; }
    public int OverdueCount { get; set; }
    public decimal OverdueAmount { get; set; }

    // Events
    public int TotalEvents { get; set; }
    public int CompletedEvents { get; set; }
    public int CancelledEvents { get; set; }

    // Quotes
    public int TotalQuotes { get; set; }
    public int ConvertedQuotes { get; set; }
    public decimal TotalQuotedValue { get; set; }
    public decimal ConversionRate { get; set; }

    // Top clients by revenue
    public List<TopClientData> TopClients { get; set; } = new();

    // Month-by-month breakdown table
    public List<MonthlyBreakdownData> MonthlyBreakdown { get; set; } = new();
}

public class TopClientData
{
    public string Name { get; set; } = "";
    public string? BusinessName { get; set; }
    public decimal Revenue { get; set; }
    public int EventCount { get; set; }
    public int InvoiceCount { get; set; }
}

public class MonthlyBreakdownData
{
    public string Month { get; set; } = "";
    public int Year { get; set; }
    public decimal Revenue { get; set; }
    public decimal Costs { get; set; }
    public decimal Profit { get; set; }
    public int EventCount { get; set; }
    public int InvoiceCount { get; set; }
}
