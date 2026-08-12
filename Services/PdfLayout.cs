using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace KiefProductions.Services.Extraction;

/// <summary>A single word with its position on the page, used for coordinate-aware matching.</summary>
public record PdfWord(string Text, double Left, double Right, double Top, double Bottom);

/// <summary>
/// A reconstructed line of text — words on roughly the same horizontal band, ordered
/// left-to-right — carrying enough position data to ask "what's near this label" rather
/// than just concatenating whatever order the PDF's content stream happens to store text in.
/// </summary>
public class PdfTextLine
{
    public required int Order { get; init; }   // reading-order index across the whole document
    public required int PageNumber { get; init; }
    public required List<PdfWord> Words { get; init; }

    public string Text => string.Join(" ", Words.Select(w => w.Text));
}

public static class PdfLayoutReader
{
    /// <summary>
    /// Reads a PDF and rebuilds it into position-aware lines across the given page range.
    /// Groups words into lines by Y-position (rounded to a small band to absorb sub-pixel
    /// baseline jitter) and orders each line left-to-right. This is the same idea as the
    /// original ExtractReadingOrderText, but it keeps the bounding boxes instead of
    /// collapsing straight to a string, so callers can reason about "same line" vs
    /// "next line" vs "far away on the page".
    /// </summary>
    public static List<PdfTextLine> ReadLines(string filePath, int maxPages = 2)
    {
        var lines = new List<PdfTextLine>();
        var order = 0;

        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages().Take(maxPages))
        {
            var words = page.GetWords()
                .Where(w => w.Letters.Count == 0 || w.Letters.All(l => l.TextOrientation == TextOrientation.Horizontal))
                .Select(w => new PdfWord(w.Text, w.BoundingBox.Left, w.BoundingBox.Right, w.BoundingBox.Top, w.BoundingBox.Bottom))
                .ToList();

            var grouped = words
                .GroupBy(w => Math.Round(w.Bottom / 3) * 3)
                .OrderByDescending(g => g.Key);

            foreach (var group in grouped)
            {
                lines.Add(new PdfTextLine
                {
                    Order = order++,
                    PageNumber = page.Number,
                    Words = group.OrderBy(w => w.Left).ToList()
                });
            }
        }

        return lines;
    }

    public static string AllText(IEnumerable<PdfTextLine> lines) => string.Join("\n", lines.Select(l => l.Text));
}