using System.Globalization;

namespace KiefProductions.Services.Extraction;

/// <summary>
/// South African documents write numeric dates as day/month/year ("22/07/2026" = 22 July).
/// A bare DateTime.TryParse uses whatever culture the process happens to be running
/// under — on most Windows dev/deploy boxes that's en-US (month/day/year), which either
/// throws out a date like "22/07/2026" outright (no 22nd month) or, worse, silently
/// swaps day and month for anything under the 13th with no error at all. Always go
/// through this instead of parsing an extracted date string directly.
/// </summary>
public static class DocumentDateParser
{
    private static readonly string[] NumericFormats =
    {
        "d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy",
        "d-M-yyyy", "dd-MM-yyyy", "d-MM-yyyy", "dd-M-yyyy",
        "d.M.yyyy", "dd.MM.yyyy", "d.MM.yyyy", "dd.M.yyyy",
    };

    private static readonly string[] WordyFormats =
    {
        "d MMMM yyyy", "dd MMMM yyyy", "d MMM yyyy", "dd MMM yyyy",
    };

    public static bool TryParse(string text, out DateTime date)
    {
        text = text.Trim();

        if (DateTime.TryParseExact(text, NumericFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        return DateTime.TryParseExact(text, WordyFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}