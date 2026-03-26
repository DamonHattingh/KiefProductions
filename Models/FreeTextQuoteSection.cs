namespace KiefProductions.Models;

public class FreeTextQuoteSection
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;
    public string CategoryName { get; set; } = string.Empty;
    public string? DescriptionHtml { get; set; }
    public decimal Qty { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Cost { get; set; }
    public int SortOrder { get; set; }
}
