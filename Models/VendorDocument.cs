using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

public class VendorDocument
{
    public int Id { get; set; }

    [Required]
    public string VendorName { get; set; } = string.Empty;

    public string? DocumentNumber { get; set; }

    public VendorDocumentType DocumentType { get; set; } = VendorDocumentType.Invoice;

    public DateTime? DocumentDate { get; set; }

    public decimal? Amount { get; set; }

    public string? Notes { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string OriginalFileName { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public DateTime UploadedDate { get; set; } = DateTime.Now;

    public int? EventId { get; set; }
    public Event? Event { get; set; }
}

public enum VendorDocumentType
{
    Invoice,
    Quote
}
