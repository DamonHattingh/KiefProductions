namespace KiefProductions.ViewModels;

public class BusinessDashboardViewModel
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Finance
    public decimal TotalRevenue { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal TotalOverdue { get; set; }
    public decimal NetProfit { get; set; }
    public decimal TotalCosts { get; set; }
    public decimal ProfitMarginPercent { get; set; }
    public decimal AverageInvoiceValue { get; set; }
    public decimal LargestInvoiceValue { get; set; }

    // Invoice status breakdown
    public int InvoicesPaid { get; set; }
    public int InvoicesSent { get; set; }
    public int InvoicesPartiallyPaid { get; set; }
    public int InvoicesOverdue { get; set; }
    public int InvoicesDraft { get; set; }
    public int TotalInvoices { get; set; }

    // Monthly chart data
    public List<MonthlyRevenueData> MonthlyRevenue { get; set; } = new();

    // Quote stats
    public int TotalQuotes { get; set; }
    public int QuotesConverted { get; set; }
    public decimal QuoteConversionRate { get; set; }
    public decimal AverageQuoteValue { get; set; }
    public decimal TotalQuotedValue { get; set; }
    public int QuotesDraft { get; set; }
    public int QuotesPending { get; set; }
    public int QuotesApproved { get; set; }
    public int QuotesRejected { get; set; }

    // Business health
    public int TotalEvents { get; set; }
    public int CompletedEvents { get; set; }
    public int TotalClientsServed { get; set; }
    public int RepeatClients { get; set; }
    public decimal RepeatClientRate { get; set; }
    public string BestMonth { get; set; } = "";
    public decimal BestMonthRevenue { get; set; }

    // Overdue aging (rand amounts)
    public decimal Overdue30Days { get; set; }
    public decimal Overdue60Days { get; set; }
    public decimal Overdue90PlusDays { get; set; }
}

public class MonthlyRevenueData
{
    public string Month { get; set; } = "";
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }
}
