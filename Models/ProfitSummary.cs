namespace KiefProductions.Models;

public class ProfitSummary
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal ProfitMargin { get; set; }
}
