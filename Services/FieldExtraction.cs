using System.Text.RegularExpressions;

namespace KiefProductions.Services.Extraction;

public record Candidate<T>(T Value, double Score, PdfTextLine Line, string Reason);

public enum ExtractionConfidence { None, Low, Medium, High }

public static class ConfidenceClassifier
{
    /// <summary>Turns a raw candidate score into a UI-friendly confidence tier.</summary>
    public static ExtractionConfidence Classify(double? score, double highThreshold, double mediumThreshold)
    {
        if (score is null) return ExtractionConfidence.None;
        if (score >= highThreshold) return ExtractionConfidence.High;
        if (score >= mediumThreshold) return ExtractionConfidence.Medium;
        return ExtractionConfidence.Low;
    }
}

/// <summary>
/// Vendor-agnostic field extraction over a set of PdfLines. Implements the pipeline:
/// find a label -> look near it (same line, then the next line or two) -> validate the
/// shape with regex -> score every candidate -> keep the best one. This is a fallback for
/// vendors you don't have a <see cref="VendorProfile"/> for — best-effort, not a guarantee.
/// The "Ranked" variants also expose runner-up candidates, so a low-confidence guess can be
/// shown alongside its alternatives instead of just silently picking one.
/// </summary>
public static class GenericFieldExtractor
{
    private static IEnumerable<PdfTextLine> NextLines(List<PdfTextLine> lines, PdfTextLine from, int count) =>
        lines.Where(l => l.Order > from.Order && l.Order <= from.Order + count).OrderBy(l => l.Order);

    // A large/bold figure (a grand total is often rendered much bigger than its label) can
    // have a taller bounding box that pushes its Y-position above the label's, which sorts
    // it *before* the label in reading order — not after. Searching only forward misses it
    // entirely. This checks a small window on both sides, ordered by actual distance.
    private static IEnumerable<PdfTextLine> NearbyLines(List<PdfTextLine> lines, PdfTextLine from, int before, int after) =>
        lines.Where(l => l.Order != from.Order && l.Order >= from.Order - before && l.Order <= from.Order + after)
             .OrderBy(l => Math.Abs(l.Order - from.Order));

    // ---- Amount (grand total) -----------------------------------------------------------

    public const double AmountHighThreshold = 2.0;
    public const double AmountMediumThreshold = 1.0;

    private static readonly Regex MoneyToken = new(@"^[R$€£]?\s*([\d]{1,3}(?:[,\s]\d{3})*\.\d{2})$", RegexOptions.IgnoreCase);

    // Order matters only for readability here — every matching label is tried, scores add up.
    private static readonly (Regex label, double weight)[] TotalLabels =
    {
        (new(@"\bprice\s+incl", RegexOptions.IgnoreCase), 3.0),
        (new(@"\btotal\s+incl", RegexOptions.IgnoreCase), 3.0),
        (new(@"\bgrand\s+total\b", RegexOptions.IgnoreCase), 3.0),
        (new(@"\binvoice\s+total\b", RegexOptions.IgnoreCase), 2.5),
        (new(@"\bamount\s+due\b", RegexOptions.IgnoreCase), 2.5),
        (new(@"\bbalance\s+due\b", RegexOptions.IgnoreCase), 2.5),
        (new(@"\btotal\s+payable\b", RegexOptions.IgnoreCase), 2.5),
        (new(@"\btotal\s+due\b", RegexOptions.IgnoreCase), 2.5),
        (new(@"\btotal\s*(zar|rand|usd|gbp|eur|dollars?)\b", RegexOptions.IgnoreCase), 2.0),
        (new(@"\bamount\s+payable\b", RegexOptions.IgnoreCase), 1.8),
        (new(@"\bplease\s+pay\b", RegexOptions.IgnoreCase), 1.8),
        (new(@"^\s*total\b", RegexOptions.IgnoreCase), 1.5), // bare "Total" / "Total:" at line start
    };

    // Deliberately does NOT include "vat" — "incl. VAT" is the label we *want*; only the
    // pre-tax / line-item / subtotal / partial-payment variants should be pushed down.
    private static readonly Regex TotalDeprioritised = new(
        @"\b(sub\s*total|excl|equipment|crew|transport|team|deposit)\b", RegexOptions.IgnoreCase);

    /// <summary>All amount candidates, best first. Use this to surface alternatives in the UI.</summary>
    public static List<Candidate<decimal>> ExtractAmountRanked(List<PdfTextLine> lines)
    {
        var candidates = new List<Candidate<decimal>>();

        foreach (var line in lines)
        {
            foreach (var (labelRegex, weight) in TotalLabels)
            {
                if (!labelRegex.IsMatch(line.Text)) continue;

                var penalty = TotalDeprioritised.IsMatch(line.Text) ? 1.5 : 0.0;

                foreach (var candidateLine in new[] { line }.Concat(NearbyLines(lines, line, 2, 4)))
                {
                    foreach (var word in candidateLine.Words)
                    {
                        var match = MoneyToken.Match(word.Text);
                        if (!match.Success) continue;
                        if (!decimal.TryParse(match.Groups[1].Value.Replace(",", "").Replace(" ", ""), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount)) continue;

                        var distancePenalty = Math.Abs(candidateLine.Order - line.Order) * 0.2;
                        candidates.Add(new Candidate<decimal>(amount, weight - penalty - distancePenalty, line, $"label '{line.Text.Trim()}'"));
                    }
                }
            }
        }

        // Best score wins; ties broken by the larger amount — a grand total incl. VAT is
        // almost always the largest figure on the page, which is a handy safety net when
        // two labels score identically (e.g. an unrecognised "Total VAT" vs the real total).
        return candidates
            .GroupBy(c => c.Value)
            .Select(g => g.OrderByDescending(c => c.Score).First())
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => c.Value)
            .ToList();
    }

    public static Candidate<decimal>? ExtractAmount(List<PdfTextLine> lines) => ExtractAmountRanked(lines).FirstOrDefault();

    // ---- Document number ------------------------------------------------------------------

    public const double DocNumberHighThreshold = 2.0;
    public const double DocNumberMediumThreshold = 1.0;

    private static readonly (Regex label, double weight)[] DocNumberLabels =
    {
        (new(@"\bquot(e|ation)\s*(no\.?|number|#)", RegexOptions.IgnoreCase), 3.0),
        (new(@"\binvoice\s*(no\.?|number|#)", RegexOptions.IgnoreCase), 3.0),
        (new(@"\bdocument\s*no\.?", RegexOptions.IgnoreCase), 2.5),
        (new(@"\border\s*(no\.?|number|#)", RegexOptions.IgnoreCase), 1.5),
        (new(@"\bjob\s*ref", RegexOptions.IgnoreCase), 1.5),
        (new(@"\byour\s+reference\b", RegexOptions.IgnoreCase), 1.0),
        // Bare "QUOTE 10898" / "INVOICE 10898" with no "No."/"Number" qualifier at all —
        // lower weight since the word alone (without a shape match right next to it) also
        // shows up harmlessly in disclaimer text ("use your quote number as reference").
        (new(@"\bquot(e|ation)\b", RegexOptions.IgnoreCase), 1.2),
        (new(@"\binvoice\b", RegexOptions.IgnoreCase), 1.2),
    };

    private static readonly Regex DocNumberShape = new(
        @"^[A-Z]{0,4}[\-\/]?\d{2,10}[A-Z0-9\-\/]*$|^[A-Z0-9]{2,4}\-\d{2,10}$", RegexOptions.IgnoreCase);

    private static readonly Regex DocNumberExclude = new(
        @"\b(vat|reg(istration)?|acc(ount)?|branch|swift|tel|phone|www|@|box)\b", RegexOptions.IgnoreCase);

    private static readonly Regex DateShapedToken = new(@"^\d{1,2}[\/\-.]\d{1,2}[\/\-.]\d{2,4}$");

    /// <summary>All document-number candidates, best first. Use this to surface alternatives in the UI.</summary>
    public static List<Candidate<string>> ExtractDocumentNumberRanked(List<PdfTextLine> lines)
    {
        var candidates = new List<Candidate<string>>();

        foreach (var line in lines)
        {
            if (DocNumberExclude.IsMatch(line.Text)) continue;

            foreach (var (labelRegex, weight) in DocNumberLabels)
            {
                if (!labelRegex.IsMatch(line.Text)) continue;

                foreach (var candidateLine in new[] { line }.Concat(NextLines(lines, line, 1)))
                {
                    if (DocNumberExclude.IsMatch(candidateLine.Text)) continue;

                    foreach (var word in candidateLine.Words)
                    {
                        if (DateShapedToken.IsMatch(word.Text)) continue;                 // not a date
                        if (!DocNumberShape.IsMatch(word.Text)) continue;
                        if (word.Text.All(char.IsDigit) && word.Text.Length >= 8) continue; // looks like a phone/VAT number

                        var distancePenalty = (candidateLine.Order - line.Order) * 0.3;
                        candidates.Add(new Candidate<string>(word.Text.Trim(), weight - distancePenalty, line, $"label '{line.Text.Trim()}'"));
                    }
                }
            }
        }

        return candidates
            .GroupBy(c => c.Value)
            .Select(g => g.OrderByDescending(c => c.Score).First())
            .OrderByDescending(c => c.Score)
            .ToList();
    }

    public static Candidate<string>? ExtractDocumentNumber(List<PdfTextLine> lines) => ExtractDocumentNumberRanked(lines).FirstOrDefault();

    // ---- Document date ----------------------------------------------------------------------

    public const double DateHighThreshold = 2.0;
    public const double DateMediumThreshold = 1.0;

    private static readonly Regex DateNumeric = new(@"\b(\d{1,2}[\/\-.]\d{1,2}[\/\-.]\d{2,4})\b");
    private static readonly Regex DateWordy = new(@"\b(\d{1,2}\s+[A-Za-z]{3,9}\s+\d{4})\b");

    private static readonly (Regex label, double weight)[] DateLabels =
    {
        (new(@"\b(quote|invoice)\s+date\b", RegexOptions.IgnoreCase), 3.0),
        (new(@"\bdated\b", RegexOptions.IgnoreCase), 2.0),
        (new(@"\bissued\b", RegexOptions.IgnoreCase), 2.0),
        (new(@"^\s*date\b", RegexOptions.IgnoreCase), 2.0),
        // Same idea as the bare-total fix — "DATE" can end up merged behind an unrelated
        // column's text on the same reconstructed line (e.g. "Kief Productions DATE
        // 13/04/2026"), which a line-start anchor can't see. Lower weight than the clean
        // start-of-line match so it only wins when nothing better is on offer.
        (new(@"\bdate\b", RegexOptions.IgnoreCase), 1.4),
    };

    // "Valid until" / "Expiry" / "Accepted date" are real dates on the page but not
    // *the* document date — excluding them stops them from stealing the top spot.
    // "Description"/"Activity"/"Qty" mark a table's column-header row (e.g. "DATE
    // DESCRIPTION TAX QTY RATE AMOUNT") — that "Date" is a column heading, not the
    // document date field, even though it also starts the line.
    private static readonly Regex DateExclude = new(
        @"\b(valid|expiry|due|accepted|deliver|description|activity|qty)\b", RegexOptions.IgnoreCase);

    public static Candidate<DateTime>? ExtractDate(List<PdfTextLine> lines)
    {
        var candidates = new List<Candidate<DateTime>>();

        foreach (var line in lines)
        {
            if (DateExclude.IsMatch(line.Text)) continue;

            foreach (var (labelRegex, weight) in DateLabels)
            {
                if (!labelRegex.IsMatch(line.Text)) continue;

                foreach (var candidateLine in new[] { line }.Concat(NextLines(lines, line, 1)))
                {
                    var match = DateNumeric.Match(candidateLine.Text);
                    if (!match.Success) match = DateWordy.Match(candidateLine.Text);
                    if (!match.Success || !DocumentDateParser.TryParse(match.Value, out var date)) continue;

                    var distancePenalty = (candidateLine.Order - line.Order) * 0.3;
                    candidates.Add(new Candidate<DateTime>(date, weight - distancePenalty, line, $"label '{line.Text.Trim()}'"));
                }
            }
        }

        if (candidates.Count == 0)
        {
            // fall back to the first unlabelled date-shaped token on the page (document
            // dates are almost always near the top; "valid until" etc. already excluded)
            foreach (var line in lines)
            {
                if (DateExclude.IsMatch(line.Text)) continue;
                var match = DateNumeric.Match(line.Text);
                if (match.Success && DocumentDateParser.TryParse(match.Value, out var date))
                {
                    candidates.Add(new Candidate<DateTime>(date, 0.5 - line.Order * 0.01, line, "unlabelled fallback"));
                    break;
                }
            }
        }

        return candidates.OrderByDescending(c => c.Score).FirstOrDefault();
    }

    // ---- Vendor name --------------------------------------------------------------------------

    public const double VendorNameHighThreshold = 1.2;
    public const double VendorNameMediumThreshold = 0.5;

    private static readonly Regex CompanySuffix = new(
        @"\b(Pty\)?\s?Ltd|CC|Inc|LLC|Productions|Events|Enterprises|International|Group|Trading|Rentals|Hire|Studios?|Media|Solutions|Suppliers|Logistics)\b",
        RegexOptions.IgnoreCase);

    // Lines introducing the *client*, not the vendor — a company-suffixed name sitting
    // right after one of these (e.g. "Company: Kief Productions") must not be picked.
    private static readonly Regex ClientBlockLabel = new(
        @"\b(company|client|bill\s*to|address|deliver\s*to|contact\s*person)\s*:?", RegexOptions.IgnoreCase);

    public static Candidate<string>? ExtractVendorName(List<PdfTextLine> lines)
    {
        var candidates = new List<Candidate<string>>();

        foreach (var line in lines.Take(15))
        {
            if (ClientBlockLabel.IsMatch(line.Text)) continue;
            if (!CompanySuffix.IsMatch(line.Text)) continue;

            var score = 2.0 - line.Order * 0.05;
            candidates.Add(new Candidate<string>(NormaliseVendorName(line.Text), score, line, "company suffix near top"));
        }

        if (candidates.Count > 0)
            return candidates.OrderByDescending(c => c.Score).First();

        var firstLine = lines.FirstOrDefault(l =>
            l.Text.Length is > 2 and < 80 &&
            !Regex.IsMatch(l.Text, @"^\d+$") &&
            !ClientBlockLabel.IsMatch(l.Text));

        return firstLine == null ? null : new Candidate<string>(NormaliseVendorName(firstLine.Text), 0.2, firstLine, "first-line fallback");
    }

    private static string NormaliseVendorName(string text)
    {
        // PDFs sometimes render "OrangeProductions" as one glued token when a decorative
        // logo overlaps the text layer — split "wordWord" boundaries back into words.
        return Regex.Replace(text.Trim(), @"(?<=[a-z])(?=[A-Z])", " ");
    }
}