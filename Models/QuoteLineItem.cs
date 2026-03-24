namespace KiefProductions.Models;

public class QuoteLineItem
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? ProductTypeId { get; set; }
    public ProductType? ProductType { get; set; }

    public int? PackageId { get; set; }
    public Package? Package { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string? ProductDescription { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public bool IsRental { get; set; }
    public bool IsExpense { get; set; }
    public LineItemType ItemType { get; set; } = LineItemType.Gear;
}

public enum LineItemType
{
    Gear,
    Rental,
    Custom,
    Expense,
    Package
}
