using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

public class Quote
{
    public int Id { get; set; }
    public string? QuoteNumber { get; set; }

    [Required]
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }

    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    public DateTime DateIssued { get; set; } = DateTime.Now;
    public DateTime DueDate { get; set; } = DateTime.Now.AddDays(30);
    public DateTime? LastSaved { get; set; }

    public QuoteType QuoteType { get; set; } = QuoteType.Itemized;

    public ICollection<QuoteLineItem> LineItems { get; set; } = new List<QuoteLineItem>();
    public ICollection<FreeTextQuoteSection> FreeTextSections { get; set; } = new List<FreeTextQuoteSection>();
    public ProfitSummary? ProfitSummary { get; set; }
    public Event? Event { get; set; }
    public Invoice? Invoice { get; set; }
}

public enum QuoteStatus
{
    Draft,
    Pending,
    Approved,
    Rejected,
    Converted
}

public enum QuoteType
{
    Itemized,
    FreeText
}
