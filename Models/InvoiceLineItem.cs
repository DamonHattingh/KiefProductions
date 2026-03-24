namespace KiefProductions.Models;

public class InvoiceLineItem
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? ProductTypeId { get; set; }
    public ProductType? ProductType { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string? ProductDescription { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public bool IsRental { get; set; }
    public LineItemType ItemType { get; set; } = LineItemType.Gear;
}
