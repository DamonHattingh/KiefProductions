using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

public class Client
{
    public int Id { get; set; }

    [Required]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    public string? Phone { get; set; }
    public string? BusinessName { get; set; }
    public string? Address { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;

    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
