using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

/// <summary>
/// Recorded whenever a person changes a field away from what the extraction service
/// guessed, before saving a VendorDocument. Not used at extraction time — this is a log to
/// review periodically (e.g. "DocumentNumber corrections are piling up for one vendor" ->
/// add a VendorProfile; "Amount corrections mention a label we don't recognise" -> add it
/// to GenericFieldExtractor's label list). Requires an EF Core migration to add the table
/// (`dotnet ef migrations add AddExtractionCorrections`) plus a DbSet on your ApplicationDbContext:
///   public DbSet&lt;ExtractionCorrection&gt; ExtractionCorrections =&gt; Set&lt;ExtractionCorrection&gt;();
/// </summary>
public class ExtractionCorrection
{
    public int Id { get; set; }

    public int? VendorDocumentId { get; set; }
    public VendorDocument? VendorDocument { get; set; }

    [Required]
    public string FieldName { get; set; } = string.Empty; // "VendorName" | "DocumentNumber" | "Amount" | "DocumentDate"

    public string? ExtractedValue { get; set; }
    public string? CorrectedValue { get; set; }

    /// <summary>The matched vendor profile name, or "generic" when the scored fallback ran.</summary>
    public string Source { get; set; } = "generic";

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}