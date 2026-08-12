using System.Globalization;
using System.Text.RegularExpressions;

namespace KiefProductions.Services.Extraction;

/// <summary>
/// A tuned rule set for one recurring supplier's exact layout. For vendors you deal with
/// repeatedly this beats generic heuristics outright — you know exactly where the total
/// sits on a Daniella Productions quote, so there's no reason to guess. New vendors fall
/// through to <see cref="GenericFieldExtractor"/> until you add a profile for them (worth
/// doing the first time you notice a vendor's fields keep coming out wrong).
/// </summary>
public class VendorProfile
{
    public required string VendorName { get; init; }
    public required Func<string, bool> Matches { get; init; } // detect from the page's raw text
    public Func<List<PdfTextLine>, decimal?>? ExtractAmount { get; init; }
    public Func<List<PdfTextLine>, string?>? ExtractDocumentNumber { get; init; }
    public Func<List<PdfTextLine>, DateTime?>? ExtractDate { get; init; }
}

public static class VendorProfileRegistry
{
    public static readonly List<VendorProfile> Profiles = new()
    {
        new VendorProfile
        {
            VendorName = "Daniella Productions",
            Matches = text => text.Contains("daniellaproductions.co.za", StringComparison.OrdinalIgnoreCase),
            ExtractAmount = lines => FindAfterLabel(lines, @"^\s*TOTAL\b"),
            ExtractDocumentNumber = lines => FindTokenAfterLabel(lines, @"QUOTE\s*NO\.?"),
            ExtractDate = lines => FindDateAfterLabel(lines, @"^\s*DATE\b"),
        },
        new VendorProfile
        {
            VendorName = "Orange Productions",
            Matches = text => text.Contains("orangeproductions.co.za", StringComparison.OrdinalIgnoreCase),
            // the payable grand total is "Price incl. VAT:" — the bare "Total:" a few lines
            // above it is the equipment + crew + transport sum, excl. VAT
            ExtractAmount = lines => FindAfterLabel(lines, @"Price\s+incl\.?\s+VAT"),
            ExtractDocumentNumber = lines => FindTokenAfterLabel(lines, @"Quote\s+Number"),
            ExtractDate = lines => FindDateAfterLabel(lines, @"Quote\s+Date"),
        },
        new VendorProfile
        {
            VendorName = "Up Beat Events",
            Matches = text => text.Contains("dancefloors.co.za", StringComparison.OrdinalIgnoreCase)
                            || text.Contains("Up Beat Events", StringComparison.OrdinalIgnoreCase),
            ExtractAmount = lines => FindAfterLabel(lines, @"^\s*TOTAL\s+ZAR\b"),
            ExtractDocumentNumber = lines => FindTokenAfterLabel(lines, @"Quote\s+Number"),
            ExtractDate = lines => FindDateAfterLabel(lines, @"^\s*Date\s*$"),
        },
        new VendorProfile
        {
            VendorName = "Dry Ice International",
            Matches = text => text.Contains("dryice.co.za", StringComparison.OrdinalIgnoreCase),
            // Sage/Pastel quote: label and value actually sit on the same reconstructed
            // line ("Total R2,080.43", "Document No QUB12522") — a plain same-line match
            // works fine here, same as any other vendor. "^\s*Total\b" deliberately does
            // NOT match "Sub Total ..." (starts with "Sub") or "Amount Excl Tax ..." so it
            // lands on the true incl-tax total.
            ExtractAmount = lines => FindAfterLabel(lines, @"^\s*Total\b"),
            ExtractDocumentNumber = lines => FindTokenAfterLabel(lines, @"Document\s*No\.?"),
            ExtractDate = lines => FindDateAfterLabel(lines, @"^\s*Date\b"),
        },
    };

    public static VendorProfile? Detect(string pageText) => Profiles.FirstOrDefault(p => p.Matches(pageText));

    // ---- shared helpers ----

    private static decimal? FindAfterLabel(List<PdfTextLine> lines, string labelPattern)
    {
        var regex = new Regex(labelPattern, RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            if (!regex.IsMatch(line.Text.Trim())) continue;

            foreach (var candidate in new[] { line }.Concat(lines.Where(l => l.Order > line.Order && l.Order <= line.Order + 2)))
            {
                var match = Regex.Match(candidate.Text, @"[\d]{1,3}(?:[,\s]\d{3})*\.\d{2}");
                if (match.Success && decimal.TryParse(match.Value.Replace(",", "").Replace(" ", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                    return amount;
            }
        }
        return null;
    }

    private static string? FindTokenAfterLabel(List<PdfTextLine> lines, string labelPattern)
    {
        var regex = new Regex(labelPattern, RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            var match = regex.Match(line.Text);
            if (!match.Success) continue;

            var after = line.Text[(match.Index + match.Length)..].Trim(' ', ':');
            var token = FirstTokenWithDigit(after) ?? after.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(token)) return token;

            // Label and value on separate lines — sometimes a stacked layout, sometimes
            // an unrelated column (e.g. the client's name) landing on the same Y-band as
            // the label by coincidence. Either way, pick the token that actually looks
            // like a code (contains a digit) rather than trusting the whole line.
            foreach (var next in lines.Where(l => l.Order > line.Order && l.Order <= line.Order + 2).OrderBy(l => l.Order))
            {
                var candidate = FirstTokenWithDigit(next.Text);
                if (!string.IsNullOrWhiteSpace(candidate)) return candidate;
            }
        }
        return null;
    }

    private static string? FirstTokenWithDigit(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(t => t.Any(char.IsDigit));

    private static readonly Regex DateShape = new(@"\d{1,2}[\/\-.]\d{1,2}[\/\-.]\d{2,4}|\d{1,2}\s+[A-Za-z]{3,9}\s+\d{4}");

    private static DateTime? FindDateAfterLabel(List<PdfTextLine> lines, string labelPattern)
    {
        var regex = new Regex(labelPattern, RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            if (!regex.IsMatch(line.Text.Trim())) continue;

            // Value can be on the same line, or up to a couple of lines below it — some
            // vendors put label and value together, others stack them (sometimes with an
            // unrelated column's text interleaved between the two).
            foreach (var candidate in new[] { line }.Concat(lines.Where(l => l.Order > line.Order && l.Order <= line.Order + 2)))
            {
                var match = DateShape.Match(candidate.Text);
                if (match.Success && DocumentDateParser.TryParse(match.Value, out var date)) return date;
            }
        }
        return null;
    }
}