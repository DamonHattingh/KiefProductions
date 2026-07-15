using System.Text.RegularExpressions;
using KiefProductions.Models;
using UglyToad.PdfPig;

namespace KiefProductions.Services;

public class VendorDocumentExtractionResult
{
    public bool TextExtracted { get; set; }
    public string? VendorName { get; set; }
    public string? DocumentNumber { get; set; }
    public VendorDocumentType? DocumentType { get; set; }
    public DateTime? DocumentDate { get; set; }
    public decimal? Amount { get; set; }
}

/// <summary>
/// Best-effort field extraction for uploaded vendor invoices/quotes using
/// regex heuristics over the text layer of a PDF. Works only on PDFs that
/// have a real (non-scanned) text layer — photos/scans are left blank for
/// manual entry.
/// </summary>
public class VendorDocumentExtractionService
{
    private static readonly Regex[] DocumentNumberPatterns =
    {
        new(@"(?:invoice|inv)\s*(?:#|no\.?|number)?\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]{2,19})", RegexOptions.IgnoreCase),
        new(@"(?:quote|quotation|qte)\s*(?:#|no\.?|number)?\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-\/]{2,19})", RegexOptions.IgnoreCase),
    };

    private static readonly Regex QuoteKeyword = new(@"\bquot(e|ation)\b", RegexOptions.IgnoreCase);
    private static readonly Regex InvoiceKeyword = new(@"\binvoice\b", RegexOptions.IgnoreCase);

    private static readonly Regex AmountPattern = new(@"(?:total|amount\s+due|balance\s+due|grand\s+total)\s*[:\-]?\s*[R$€£]?\s*([\d,]+\.\d{2})", RegexOptions.IgnoreCase);

    private static readonly Regex DatePattern = new(@"\b(\d{1,2}[\/\-.]\d{1,2}[\/\-.]\d{2,4}|\d{4}[\/\-.]\d{1,2}[\/\-.]\d{1,2})\b");

    public VendorDocumentExtractionResult Extract(string filePath, string? contentType)
    {
        var result = new VendorDocumentExtractionResult();

        if (!IsPdf(filePath, contentType))
            return result;

        string text;
        try
        {
            using var document = PdfDocument.Open(filePath);
            var pages = document.GetPages().Take(2); // header info is almost always on page 1
            text = string.Join("\n", pages.Select(p => p.Text));
        }
        catch
        {
            // Unreadable / encrypted / corrupt PDF — leave everything blank for manual entry
            return result;
        }

        if (string.IsNullOrWhiteSpace(text))
            return result;

        result.TextExtracted = true;

        // Document type: look for "quote"/"quotation" vs "invoice" mentions
        result.DocumentType = QuoteKeyword.IsMatch(text) && !InvoiceKeyword.IsMatch(text)
            ? VendorDocumentType.Quote
            : VendorDocumentType.Invoice;

        // Document number
        foreach (var pattern in DocumentNumberPatterns)
        {
            var match = pattern.Match(text);
            if (match.Success)
            {
                result.DocumentNumber = match.Groups[1].Value.Trim();
                break;
            }
        }

        // Vendor/company name: best-effort guess — first reasonably-sized line of text,
        // which on most letterhead invoices is the company name.
        var firstLine = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(l => l.Length is > 2 and < 80 && !Regex.IsMatch(l, @"^\d+$"));
        result.VendorName = firstLine;

        // Amount
        var amountMatch = AmountPattern.Match(text);
        if (amountMatch.Success && decimal.TryParse(amountMatch.Groups[1].Value.Replace(",", ""), out var amount))
            result.Amount = amount;

        // Date
        var dateMatch = DatePattern.Match(text);
        if (dateMatch.Success && DateTime.TryParse(dateMatch.Value, out var date))
            result.DocumentDate = date;

        return result;
    }

    private static bool IsPdf(string filePath, string? contentType)
    {
        return contentType == "application/pdf" || filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    }
}
