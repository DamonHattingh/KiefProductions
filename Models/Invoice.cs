using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

public class Invoice
{
    public int Id { get; set; }
    public string? InvoiceNumber { get; set; }

    [Required]
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public int? QuoteId { get; set; }
    public Quote? Quote { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue => Total - AmountPaid;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime DateIssued { get; set; } = DateTime.Now;
    public DateTime DueDate { get; set; } = DateTime.Now.AddDays(30);

    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public enum InvoiceStatus
{
    Draft,
    Sent,
    PartiallyPaid,
    Paid,
    Overdue
}
