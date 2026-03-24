using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

public class Payment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    [Required]
    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.Now;
    public string? Method { get; set; }
    public string? Notes { get; set; }
}
